using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChildOrganiser : MonoBehaviour
{
    [SerializeField] public bool toCentreChildren = true;
    [SerializeField] public float gap = 0.5f;

    public void UpdateChildren()
    {
        IOrganiserChild[] activeChildren = children.Where(child => child.GetVisible()).ToArray();

        float totalHeight = activeChildren.Sum(child => child.GetHeight());

        // TODO: Why is this not doing anything? where is it used
        for (int i = 0; i < activeChildren.Length; i++)
        {
            activeChildren[i].GetTransform().localPosition = Vector3.zero;
        }
    }

    public void Clear()
    {
        foreach (Transform child in transform) DestroyImmediate(child.gameObject);
        children.Clear();
    }

    public void AddChild(IOrganiserChild newChild)
    {
        children.Add(newChild);
        newChild.GetTransform().parent = transform;
    }

    private List<IOrganiserChild> children = new List<IOrganiserChild>();

    private void Update() => UpdateChildren();
}
