using GK;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GK.VoronoiClipper;

public partial class VoronoiMeshGenerator : Generator
{
    public override string Name => "Voronoi Mesh";
    public MeshSite[] MeshSites { get; private set; }
    public Mesh Mesh { get; private set; }

    public void SafeGenerate(MeshFilter meshFilter, PolygonCollider2D outsidePolygon, int seedCount, float seedMinDistance)
    {
        // Keep trying Generate and catch errors
        int tries = 0;
        while (tries < pipelineMaxTries)
        {
            try
            {
                Generate(meshFilter, outsidePolygon, seedCount, seedMinDistance);
                break;
            }
            catch (Exception e)
            {
                tries++;
                Debug.LogException(e);
                Debug.LogWarning("Pipeline threw error " + tries + "/" + pipelineMaxTries + ".");
            }
        }

        // Hit max number of tries
        if (tries == pipelineMaxTries)
        {
            throw new Exception("Voronoi Mesh Pipeline hit maximum number of tries (" + tries + "/" + pipelineMaxTries + ").");
        }
    }

    public void Generate(MeshFilter meshFilter, PolygonCollider2D outsidePolygon, int seedCount, float seedMinDistance)
    {
        this.meshFilter = meshFilter;
        this.outsidePolygon = outsidePolygon;
        this.seedCount = seedCount;
        this.seedMinDistance = seedMinDistance;
        Generate();
    }

    public override void Generate()
    {
        Clear();
        StepGenerateSeeds();
        StepGenerateVoronoi();
        StepClipSites();
        StepGenerateMeshAndSites();
        StepProcessSites();
        if (clearInternal) ClearInternal();
        IsGenerated = true;
    }

    public override void Clear()
    {
        ClearInternal();
        ClearOutput();
        IsGenerated = false;
    }

    public void ClearInternal()
    {
        // Clear internal variables
        voronoiSeedSites = null;
        voronoiCalculator = null;
        voronoiDiagram = null;
        clipperPolygon = null;
        voronoiClipper = null;
    }

    public void ClearOutput()
    {
        // Clear external variables
        if (Mesh != null) Mesh.Clear();
        MeshSites = null;
        Mesh = null;
    }

    [Header("Gizmos")]
    [SerializeField] private bool showGizmoSeedCentroids = false;
    [SerializeField] private bool showGizmoDelauneyMain = false;
    [SerializeField] private bool showGizmoDelauneyCentres = false;
    [SerializeField] private bool showGizmoVoronoi = false;
    [SerializeField] private bool showGizmoClipped = false;
    [SerializeField] private bool showGizmoMesh = false;

    [Header("Parameters")]
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private PolygonCollider2D outsidePolygon;
    [SerializeField] private int seedCount;
    [SerializeField] private float seedMinDistance;
    [SerializeField] private int pipelineMaxTries = 10;
    [SerializeField] private int seedMaxTries = 50;
    [SerializeField] private bool clearInternal = true;
    private Vector2[] voronoiSeedSites;
    private VoronoiCalculator voronoiCalculator;
    private VoronoiDiagram voronoiDiagram;
    private List<Vector2> clipperPolygon;
    private VoronoiClipper voronoiClipper;

    private void StepGenerateSeeds()
    {
        // Generate a set number of seeds
        List<Vector2> voronoiSeedSiteList = new List<Vector2>();
        int tries = 0;
        while (voronoiSeedSiteList.Count < seedCount && tries < seedMaxTries)
        {
            // Generate in a random position
            Vector2 seedWorld = Utility.RandomInPolygon(outsidePolygon, tries != 0);
            Vector2 seedLocal = outsidePolygon.transform.InverseTransformPoint(seedWorld);
            bool isValid = true;
            tries++;

            // Check minimum distance to each other seed
            foreach (Vector2 seed in voronoiSeedSiteList)
            {
                float dst = Vector3.Distance(seed, seedLocal);
                if (dst < seedMinDistance) { isValid = false; break; }
            }

            // If found a valid seed then add to list
            if (isValid)
            {
                tries = 0;
                voronoiSeedSiteList.Add(seedLocal);
                continue;
            }
        }

        // If hit max number of tries
        if (tries == seedMaxTries) Debug.LogWarning("Hit max number of tries (" + voronoiSeedSiteList.Count + "/" + seedCount + ")");

        // Update seed sites
        voronoiSeedSites = voronoiSeedSiteList.ToArray();
    }

    private void StepGenerateVoronoi()
    {
        // Perform voronoi calculation
        voronoiCalculator = new VoronoiCalculator();
        voronoiDiagram = voronoiCalculator.CalculateDiagram(voronoiSeedSites);

        // Check if generated any NaN vertices
        int badGeneratedVertices = 0;
        foreach (Vector2 v in voronoiDiagram.Vertices)
        {
            if (!VectorExtensions.IsReal(v)) badGeneratedVertices++;
        }
        if (badGeneratedVertices > 0)
        {
            throw new Exception("Voronoi generated " + badGeneratedVertices + " (NaN, NaN) vertices. This is likely due to LineLineIntersection determinant threshold not being low enough.");
        }
    }

    private void StepClipSites()
    {
        // Initialize variables
        clipperPolygon = outsidePolygon.points.ToList();
        voronoiClipper = new VoronoiClipper();
        voronoiClipper.ClipDiagram(voronoiDiagram, clipperPolygon);

        // Check for bad clipped sites
        int badClippedSites = 0;
        foreach (var site in voronoiClipper.clippedSites)
        {
            if (site.clippedVertices.Count == 0) badClippedSites++;
        }
        if (badClippedSites > 0)
        {
            throw new Exception("Clipper clipped " + badClippedSites + " sites to nothing. This is likely due to InsideCircumcircle threshold not being low enough.");
        }
    }

    private void StepGenerateMeshAndSites()
    {
        // Setup all mesh data variables
        MeshSites = new MeshSite[voronoiClipper.clippedSites.Count];
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        Vector2[] polygonPoints = outsidePolygon.points;

        // Extract mesh variables
        for (int i = 0; i < voronoiClipper.clippedSites.Count; i++)
        {
            // Add centroid vertex / uv / normal / color
            ClippedSite clippedSite = voronoiClipper.clippedSites[i];
            int centroidVertexI = vertices.Count;
            Vector2 centroidLocal = clippedSite.clippedCentroid;
            vertices.Add(centroidLocal);
            normals.Add(Vector3.back);
            float centroidAngle = Vector2.Angle(centroidLocal, Vector2.up);
            uvs.Add(new Vector3(Utility.DistanceToPoints(centroidLocal, polygonPoints), centroidAngle, 0));

            // Generate MeshSite
            MeshSite meshSite = new MeshSite
            {
                siteIdx = i,
                vertices = new MeshSiteVertex[clippedSite.clippedVertices.Count],
                edges = new MeshSiteEdge[clippedSite.clippedVertices.Count],
                meshVerticesIdx = new int[clippedSite.clippedVertices.Count],
                meshCentroidIdx = centroidVertexI,
                neighbouringSitesIdx = new HashSet<int>()
            };
            MeshSites[i] = meshSite;

            // Add vertices vertex / uv / normal / color
            for (int o = 0; o < clippedSite.clippedVertices.Count; o++)
            {
                Vector2 vertexLocal = clippedSite.clippedVertices[o].vertex;
                vertices.Add(vertexLocal);
                normals.Add(Vector3.back);
                float vertexAngle = Vector2.Angle(vertexLocal, Vector2.up);
                uvs.Add(new Vector3(Utility.DistanceToPoints(vertexLocal, polygonPoints), vertexAngle, 0));

                // Add triangle
                int siteVertexIdx = (o);
                int siteNextVertexIdx = (o + 1) % clippedSite.clippedVertices.Count;
                int meshVertexI = (centroidVertexI + 1) + siteVertexIdx;
                int nextMeshVertexI = (centroidVertexI + 1) + siteNextVertexIdx;
                triangles.Add(centroidVertexI);
                triangles.Add(nextMeshVertexI);
                triangles.Add(meshVertexI);

                // Populate MeshSite
                meshSite.vertices[o] = new MeshSiteVertex(clippedSite.clippedVertices[o]);
                meshSite.edges[o] = new MeshSiteEdge(i, siteVertexIdx, siteNextVertexIdx);
                meshSite.meshVerticesIdx[o] = meshVertexI;
            }
        }

        // Assign mesh variables
        Mesh = new Mesh
        {
            vertices = vertices.ToArray(),
            normals = normals.ToArray(),
            uv = uvs.ToArray(),
            triangles = triangles.ToArray()
        };
        meshFilter.mesh = Mesh;
    }

    private void StepProcessSites()
    {
        // Add neighbours using delauney
        bool addNeighbours(int s0, int s1)
        {
            MeshSites[s0].neighbouringSitesIdx.Add(s1);
            MeshSites[s1].neighbouringSitesIdx.Add(s0);
            return true;
        }
        List<int> tris = voronoiDiagram.Triangulation.Triangles;
        for (int ti = 0; ti < tris.Count; ti += 3)
        {
            int v0 = tris[ti];
            int v1 = tris[ti + 1];
            int v2 = tris[ti + 2];
            addNeighbours(v0, v1);
            addNeighbours(v1, v2);
            addNeighbours(v2, v0);
        }

        // Populate each sites edge infos
        foreach (MeshSite meshSite in MeshSites)
        {
            for (int i = 0; i < meshSite.vertices.Length; i++)
            {
                // - May have already populated so skip
                MeshSiteEdge edge = meshSite.edges[i];
                if (edge.isOutside || edge.neighbouringSiteIdx != -1) continue;

                // Check if outside
                int nextI = (i + 1) % meshSite.vertices.Length;
                MeshSiteVertex v0 = meshSite.vertices[i];
                MeshSiteVertex v1 = meshSite.vertices[nextI];
                edge.isOutside = (
                    (v0.type == VertexType.Polygon && v1.type == VertexType.Polygon)
                    || (v0.type == VertexType.PolygonIntersection && v1.type == VertexType.Polygon && v0.intersectionToUID == v1.vertexUID)
                    || (v0.type == VertexType.Polygon && v1.type == VertexType.PolygonIntersection && v0.vertexUID == v1.intersectionFromUID)
                    || (v0.type == VertexType.PolygonIntersection && v1.type == VertexType.PolygonIntersection && v0.intersectionFromUID == v1.intersectionFromUID));
                meshSite.isOutside |= edge.isOutside;

                // Inside so find touching edge
                if (!edge.isOutside)
                {
                    // - Loop over each other sites edges
                    foreach (MeshSite oMeshSite in MeshSites)
                    {
                        if (meshSite == oMeshSite) continue;
                        bool hasFound = false;
                        ClippedSite oClippedSite = voronoiClipper.clippedSites[oMeshSite.siteIdx];
                        for (int o = 0; o < oClippedSite.clippedVertices.Count; o++)
                        {
                            int nextO = (o + 1) % oClippedSite.clippedVertices.Count;
                            ClippedVertex ov0 = oClippedSite.clippedVertices[o];
                            ClippedVertex ov1 = oClippedSite.clippedVertices[nextO];

                            // - Check if match in either direction
                            if (
                                v0.vertexUID == ov0.vertexUID && v1.vertexUID == ov1.vertexUID
                                || v0.vertexUID == ov1.vertexUID && v1.vertexUID == ov0.vertexUID
                            )
                            {
                                edge.neighbouringSiteIdx = oMeshSite.siteIdx;
                                edge.neighbouringEdgeIdx = o;
                                oMeshSite.edges[o].neighbouringSiteIdx = meshSite.siteIdx;
                                oMeshSite.edges[o].neighbouringEdgeIdx = i;
                                hasFound = true;
                                break;
                            }
                        }
                        if (hasFound) break;
                    }

                    // - Should have found edge
                    if (edge.neighbouringSiteIdx == -1)
                    {
                        throw new Exception("Site " + meshSite.siteIdx + " edge " + i + " Could not find any neighbouring sites.");
                    }
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        // Draw all the seed points
        if (showGizmoSeedCentroids && voronoiSeedSites != null)
        {
            Gizmos.color = Color.red;
            foreach (var seed in voronoiSeedSites)
            {
                Vector2 seedWorld = transform.TransformPoint(seed);
                Gizmos.DrawSphere(seedWorld, 0.02f);
            }
        }

        // Draw delauney triangulation
        if (showGizmoDelauneyMain && voronoiDiagram != null && voronoiDiagram.Triangulation != null)
        {
            var tris = voronoiDiagram.Triangulation.Triangles;
            var verts = voronoiDiagram.Triangulation.Vertices;
            Gizmos.color = Color.red;
            foreach (var site in verts)
            {
                Vector2 siteWorld = transform.TransformPoint(site);
                Gizmos.DrawSphere(siteWorld, 0.035f);
            }
            for (int ti = 0; ti < tris.Count; ti += 3)
            {
                var p0 = verts[tris[ti]];
                var p1 = verts[tris[ti + 1]];
                var p2 = verts[tris[ti + 2]];
                var p0w = transform.TransformPoint(p0);
                var p1w = transform.TransformPoint(p1);
                var p2w = transform.TransformPoint(p2);
                Gizmos.color = Color.black;
                Gizmos.DrawLine(p0w, p1w);
                Gizmos.DrawLine(p1w, p2w);
                Gizmos.DrawLine(p2w, p0w);

                if (showGizmoDelauneyCentres)
                {
                    var cw = transform.TransformPoint(Geom.CircumcircleCenter(p0, p1, p2));
                    Gizmos.color = Color.black;
                    Gizmos.DrawCube(cw, Vector3.one * 0.02f);
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(p0w, cw);
                    Gizmos.DrawLine(p1w, cw);
                    Gizmos.DrawLine(p2w, cw);
                }
            }
        }

        // Draw unclipped voronoi points
        if (showGizmoVoronoi && voronoiDiagram != null)
        {
            for (int i = 0; i < voronoiDiagram.Sites.Count; i++)
            {
                // Draw sphere
                Gizmos.color = Color.red;
                Vector2 siteWorld = transform.TransformPoint(voronoiDiagram.Sites[i]);
                Gizmos.DrawSphere(siteWorld, 0.05f);

                // Find first / last edge of site
                int firstEdge = voronoiDiagram.FirstEdgeBySite[i];
                int lastEdge;
                if (i == voronoiDiagram.Sites.Count - 1) lastEdge = voronoiDiagram.Edges.Count - 1;
                else lastEdge = voronoiDiagram.FirstEdgeBySite[i + 1] - 1;

                // Loop over each edge and extract start / direction
                for (int ei = firstEdge; ei <= lastEdge; ei++)
                {
                    var edge = voronoiDiagram.Edges[ei];
                    Vector2 lv, ld;

                    // - Edge is ray so take direction
                    if (edge.Type == VoronoiDiagram.EdgeType.RayCCW || edge.Type == VoronoiDiagram.EdgeType.RayCW)
                    {
                        lv = transform.TransformPoint(voronoiDiagram.Vertices[edge.Vert0]);
                        ld = edge.Direction;
                        if (edge.Type == VoronoiDiagram.EdgeType.RayCW) ld *= -1;
                        Gizmos.color = Color.blue;
                        Gizmos.DrawCube(lv, Vector3.one * 0.04f);
                        Gizmos.color = Color.grey;
                        Gizmos.DrawLine(lv, lv + ld.normalized);
                    }

                    // - Edge is segment so create direction
                    else if (edge.Type == VoronoiDiagram.EdgeType.Segment)
                    {
                        var lcv0 = transform.TransformPoint(voronoiDiagram.Vertices[edge.Vert0]);
                        var lcv1 = transform.TransformPoint(voronoiDiagram.Vertices[edge.Vert1]);
                        Gizmos.color = Color.blue;
                        Gizmos.DrawCube(lcv0, Vector3.one * 0.04f);
                        Gizmos.DrawCube(lcv1, Vector3.one * 0.04f);
                        Gizmos.color = Color.white;
                        Gizmos.DrawLine(lcv0, lcv1);
                    }
                }
            }
        }

        // Draw clipped points
        if (showGizmoClipped && voronoiClipper != null)
        {
            foreach (var clippedSite in voronoiClipper.clippedSites)
            {
                Vector2 siteWorld = transform.TransformPoint(clippedSite.clippedCentroid);
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(siteWorld, 0.075f);

                foreach (var vertex in clippedSite.clippedVertices)
                {
                    Vector2 vertexWorld = transform.TransformPoint(vertex.vertex);
                    if (vertex.type == VoronoiClipper.VertexType.Polygon) Gizmos.color = Color.black;
                    else if (vertex.type == VoronoiClipper.VertexType.PolygonIntersection) Gizmos.color = Color.magenta;
                    else if (vertex.type == VoronoiClipper.VertexType.SiteVertex) Gizmos.color = Color.green;
                    Gizmos.DrawCube(vertexWorld, Vector3.one * 0.04f);
                }
            }
        }

        // Draw mesh sites
        if (showGizmoMesh && MeshSites != null)
        {
            foreach (MeshSite meshSite in MeshSites)
            {
                Vector2 centroidWorld = transform.TransformPoint(Mesh.vertices[meshSite.meshCentroidIdx]);
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(centroidWorld, 0.1f);

                Gizmos.color = Color.blue;
                foreach (MeshSiteEdge edge in meshSite.edges)
                {
                    if (edge.isOutside)
                    {
                        Vector2 edge0World = transform.TransformPoint(Mesh.vertices[MeshSites[edge.siteIndex].meshVerticesIdx[edge.siteFromVertexIdx]]);
                        Vector2 edge1World = transform.TransformPoint(Mesh.vertices[MeshSites[edge.siteIndex].meshVerticesIdx[edge.siteToVertexIdx]]);
                        Gizmos.DrawCube((edge0World + edge1World) / 2, Vector3.one * 0.1f);
                    }
                }
            }
        }
    }
}
