using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class FeatureRule
{
    [SerializeField] public bool isGuaranteed;
    [SerializeField] public float averagePer100;
    [SerializeField] public float minDistance;
    [SerializeField] public GameObject feature;
};

[CreateAssetMenu(fileName = "Surface Biome", menuName = "Surface Biome")]
public class SurfaceBiome : Biome
{
    // Features generated by WorldBiomeGenerator.StepPopulateBiomes()
    [SerializeField] private FeatureRule[] frontDecorRules = new FeatureRule[0];
    [SerializeField] private FeatureRule[] terrainRules = new FeatureRule[0];
    [SerializeField] private FeatureRule[] foregroundRules = new FeatureRule[0];
    [SerializeField] private FeatureRule[] backgroundRules = new FeatureRule[0];
    [SerializeField] private FeatureRule[] backDecorRules = new FeatureRule[0];

    private void OnValidate()
    {
        Rules ??= new Dictionary<GameLayer, FeatureRule[]>();
        Rules[GameLayer.FRONT_DECOR] = frontDecorRules;
        Rules[GameLayer.TERRAIN] = terrainRules;
        Rules[GameLayer.FOREGROUND] = foregroundRules;
        Rules[GameLayer.BACKGROUND] = backgroundRules;
        Rules[GameLayer.BACK_DECOR] = backDecorRules;
    }

    public Dictionary<GameLayer, FeatureRule[]> Rules { get; private set; }
}
