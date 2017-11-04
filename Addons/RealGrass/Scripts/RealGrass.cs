// Project:         Real Grass for Daggerfall Unity
// Web Site:        http://forums.dfworkshop.net/viewtopic.php?f=14&t=17
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/TheLacus/realgrass-du-mod
// Original Author: Uncanny_Valley (original Real Grass)
// Contributors:    TheLacus (Water plants, mod version and improvements) 
//                  Midopa

// #define TEST_PERFORMANCE

using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Utility;

namespace RealGrass
{
    /// <summary>
    /// Real Grass creates detail prototypes layers on terrain to place various components:
    ///
    /// GRASS
    /// Adds a layer of tufts of grass, to give the impression of a grassy meadow. 
    /// There are two variants of grass, varying for different regions.
    ///
    /// WATER PLANTS 
    /// Places vegetation near water areas, like lakes and river.
    /// There is a total of four different plants, for the same number of climate regions: mountain, temperate, 
    /// desert and swamp. They all have two variations, summer and winter. 
    /// Additionally it places waterlilies above the surface of temperate lakes and some tufts 
    /// of grass inside the mountain water zones.
    /// Plants bend in the wind and waterlilies move slightly on the water surface moved by the same wind. 
    ///
    /// STONES
    /// Places little stones on the cultivated grounds near farms. 
    /// 
    /// FLOWERS
    /// Places flowers on grass terrain.
    ///
    /// Real Grass thread on DU forums:
    /// http://forums.dfworkshop.net/viewtopic.php?f=14&t=17
    /// </summary>
    public class RealGrass : MonoBehaviour
    {
        #region Fields

        static RealGrass instance;
        static Mod mod;
        static ModSettings settings;

        bool isEnabled;

        DetailPrototypesCreator detailPrototypesCreator;
        DetailPrototypesDensity detailPrototypesDensity;

        // Optional details
        bool waterPlants, winterPlants, terrainStones, flowers;

        // Terrain settings
        float detailObjectDistance;
        float detailObjectDensity;

        #endregion

        #region Properties

        // Singleton
        public static RealGrass Instance
        {
            get
            {
                if (instance == null)
                    instance = FindObjectOfType<RealGrass>();
                return instance;
            }
        }

        public static Mod Mod
        {
            get { return mod; }
        }

        public static ModSettings Settings
        {
            get { return settings; }
        }

        /// <summary>
        /// Resources folder on disk.
        /// </summary>
        public static string ResourcesFolder
        {
            get { return Path.Combine(mod.DirPath, "Resources"); }
        }

        // Details status
        public bool WaterPlants { get { return waterPlants; } }
        public bool WinterPlants { get { return winterPlants; } }
        public bool TerrainStones { get { return terrainStones; } }
        public bool Flowers { get { return flowers; } }

        /// <summary>
        /// Details will be rendered up to this distance from the player.
        /// </summary>
        public float DetailObjectDistance
        {
            get { return detailObjectDistance; }
            set { detailObjectDistance = value; }
        }

        #endregion

        #region Unity

        /// <summary>
        /// Mod loader.
        /// </summary>
        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            // Get mod
            mod = initParams.Mod;

            // Add script to the scene.
            GameObject go = new GameObject("RealGrass");
            instance = go.AddComponent<RealGrass>();

            // After finishing, set the mod's IsReady flag to true.
            mod.IsReady = true;
        }

        void Awake()
        {
            if (instance != null && this != instance)
                Destroy(this.gameObject);

            Debug.LogFormat("{0} started.", this);
        }

        void Start()
        {
            StartMod(true, false);
            isEnabled = true;

            RealGrassConsoleCommands.RegisterCommands();
        }

        #endregion

        #region Public Methods

        public override string ToString()
        {
            if (mod == null)
                return base.ToString();

            return string.Format("{0} v.{1}", mod.Title, mod.ModInfo.ModVersion);
        }

        /// <summary>
        /// Toggle mod and add/remove grass on existing terrains.
        /// </summary>
        /// <returns>New status of mod.</returns>
        public bool ToggleMod()
        {
            ToggleMod(!isEnabled);
            return isEnabled;
        }

        /// <summary>
        /// Set status of mod and add/remove grass on existing terrains.
        /// </summary>
        /// <param name="enable">New status to set.</param>
        public void ToggleMod(bool enable)
        {
            if (isEnabled == enable)
                return;

            if (enable)
                StartMod(false, true);
            else
                StopMod();

            isEnabled = enable;
        }

        /// <summary>
        /// Restart mod to apply changes.
        /// </summary>
        public void RestartMod()
        {
            if (enabled)
                StopMod();

            StartMod(false, true);

            isEnabled = true;
        }

        #endregion

        #region Mod Logic

        /// <summary>
        /// Add Grass and other details on terrain.
        /// </summary>
        private void AddGrass(DaggerfallTerrain daggerTerrain, TerrainData terrainData)
        {

#if TEST_PERFORMANCE

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

#endif

            // Terrain settings 
            InitTerrain(daggerTerrain, terrainData);
            Color32[] tilemap = daggerTerrain.TileMap;

            // Get the current season and climate
            var currentSeason = DaggerfallUnity.Instance.WorldTime.Now.SeasonValue;
            int currentClimate = daggerTerrain.MapData.worldClimate;

            // Update detail layers
            detailPrototypesDensity.InitDetailsLayers();
            if (currentClimate > 225 && currentClimate != Climate.Desert3)
            {
                if (currentSeason != DaggerfallDateTime.Seasons.Winter)
                {
                    // Summer
                    detailPrototypesCreator.UpdateClimateSummer(currentClimate);
                    detailPrototypesDensity.SetDensitySummer(tilemap, currentClimate);
                }
                else if (waterPlants && winterPlants)
                {
                    // Winter
                    detailPrototypesCreator.UpdateClimateWinter(currentClimate);
                    detailPrototypesDensity.SetDensityWinter(tilemap);
                }
            }
            else if (waterPlants && 
                (currentClimate == Climate.Desert || currentClimate == Climate.Desert2 || currentClimate == Climate.Desert3))
            {
                // Desert
                detailPrototypesCreator.UpdateClimateDesert();
                detailPrototypesDensity.SetDensityDesert(tilemap);
            }

            // Assign detail prototypes to the terrain
            terrainData.detailPrototypes = detailPrototypesCreator.DetailPrototypes;
            Indices indices = detailPrototypesCreator.Indices;

            // Assign detail layers to the terrain
            terrainData.SetDetailLayer(0, 0, indices.Grass, detailPrototypesDensity.Grass); // Grass
            if (waterPlants)
            {
                terrainData.SetDetailLayer(0, 0, indices.WaterPlants, detailPrototypesDensity.WaterPlants); // Water plants near water
                terrainData.SetDetailLayer(0, 0, indices.Waterlilies, detailPrototypesDensity.Waterlilies); // Waterlilies and grass inside water
            }
            if (terrainStones)
                terrainData.SetDetailLayer(0, 0, indices.Stones, detailPrototypesDensity.Stones); // Stones
            if (flowers)
                terrainData.SetDetailLayer(0, 0, indices.Flowers, detailPrototypesDensity.Flowers); // Flowers 

#if TEST_PERFORMANCE

            stopwatch.Stop();
            Debug.LogFormat("RealGrass - Time elapsed: {0} ms.", stopwatch.Elapsed.Milliseconds);

#endif
        }

        /// <summary>
        /// Set settings for terrain.
        /// </summary>
        private void InitTerrain(DaggerfallTerrain daggerTerrain, TerrainData terrainData)
        {
            // Resolution of the detail map
            terrainData.SetDetailResolution(256, 8);

            // Grass max distance and density
            Terrain terrain = daggerTerrain.gameObject.GetComponent<Terrain>();
            terrain.detailObjectDistance = detailObjectDistance;
            terrain.detailObjectDensity = detailObjectDensity;

            // Waving grass tint
            terrainData.wavingGrassTint = Color.gray;

            // Set seed for terrain
            Random.InitState(TerrainHelper.MakeTerrainKey(daggerTerrain.MapPixelX, daggerTerrain.MapPixelY));
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Start mod and optionally add grass to existing terrains.
        /// </summary>
        /// <param name="loadSettings">Load user settings.</param>
        /// <param name="initTerrains">Add Grass to existing terrains (unnecessary at startup).</param>
        private void StartMod(bool loadSettings, bool initTerrains)
        {
            if(loadSettings)
            {
                try
                {
                    // Load settings and setup components
                    LoadSettings();
                    detailPrototypesCreator = new DetailPrototypesCreator();
                    detailPrototypesDensity = new DetailPrototypesDensity();
                }
                catch (System.Exception e)
                {
                    Debug.LogErrorFormat("RealGrass: Failed to setup mod as per user settings!\n{0}", e.ToString());
                    return;
                }
            }

            // Subscribe to onPromoteTerrainData
            DaggerfallTerrain.OnPromoteTerrainData += AddGrass;

            // Place details on existing terrains
            if(initTerrains)
                StartCoroutine(InitTerrains());

            Debug.Log("Real Grass is now enabled; subscribed to terrain promotion.");
        }

        private IEnumerator InitTerrains()
        {
            var terrains = GameManager.Instance.StreamingWorld.StreamingTarget.GetComponentsInChildren<DaggerfallTerrain>();
            foreach (DaggerfallTerrain daggerTerrain in terrains)
            {
                AddGrass(daggerTerrain, daggerTerrain.gameObject.GetComponent<Terrain>().terrainData);
                yield return new WaitForEndOfFrame();
            }
        }

        /// <summary>
        /// Stop mod and remove grass fom existing terrains.
        /// </summary>
        private void StopMod()
        {
            // Unsubscribe from onPromoteTerrainData
            DaggerfallTerrain.OnPromoteTerrainData -= AddGrass;

            // Remove details from terrains
            Terrain[] terrains = GameManager.Instance.StreamingWorld.StreamingTarget.GetComponentsInChildren<Terrain>();
            foreach (TerrainData terrainData in terrains.Select(x => x.terrainData))
            {
                for (int i = 0; i < 5; i++)
                    terrainData.SetDetailLayer(0, 0, i, detailPrototypesDensity.Empty);
                terrainData.detailPrototypes = null;
            }

            Debug.Log("Real Grass is now disabled; unsubscribed from terrain promotion.");
        }

        /// <summary>
        /// Load settings.
        /// </summary>
        private void LoadSettings()
        {
            const string waterPlantsSection = "WaterPlants", stonesSection = "TerrainStones", flowersSection = "Flowers";

            // Load settings
            settings = new ModSettings(mod);

            // Optional details
            int waterPlantsMode = settings.GetInt(waterPlantsSection, "Mode", 0, 2);
            waterPlants = waterPlantsMode != 0;
            winterPlants = waterPlantsMode == 2;
            terrainStones = settings.GetBool(stonesSection, "Enable");
            flowers = settings.GetBool(flowersSection, "Enable");

            // Terrain
            const string terrainSection = "Terrain";
            detailObjectDistance = settings.GetFloat(terrainSection, "DetailDistance", 10f);
            detailObjectDensity = settings.GetFloat(terrainSection, "DetailDensity", 0.1f, 1f);
        }

        #endregion
    }
}