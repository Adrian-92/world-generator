using Godot;
using System;
using System.Collections.Generic;

public partial class WorldGenerator : Node
{

	[Export] public int MapHeight = 256;
	private FastNoiseLite _tempNoise = new FastNoiseLite();
	private FastNoiseLite _moistureNoise = new FastNoiseLite();

	private FastNoiseLite _noise = new FastNoiseLite();
	private FastNoiseLite _oreNoise = new FastNoiseLite();
	private FastNoiseLite _oreClusterMask = new FastNoiseLite();
	private FastNoiseLite _caveNoise = new FastNoiseLite();
	private FastNoiseLite _caveClusterMask = new FastNoiseLite();
	private FastNoiseLite _waterNoise = new FastNoiseLite();
	private FastNoiseLite _waterClusterMask = new FastNoiseLite();

	public void SetupNoise(int mapSeed, float noiseFrequency) {
		_tempNoise.Seed = mapSeed;
		_tempNoise.Frequency = 0.005f;
		_moistureNoise.Seed = mapSeed + 123;
		_moistureNoise.Frequency = 0.005f;

		_noise.Seed = mapSeed;
		_noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noise.Frequency = noiseFrequency;
		_noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noise.FractalOctaves = 4;
		_noise.DomainWarpEnabled = false;
		_noise.DomainWarpAmplitude = 20.0f;

		_oreNoise.Seed = mapSeed + 420;
		_oreNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
		_oreNoise.Frequency = 0.1f;
		_oreNoise.CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance;

		_oreClusterMask.Seed = mapSeed + 69;
		_oreClusterMask.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_oreClusterMask.Frequency = 0.06f;

		_caveNoise.Seed = mapSeed + 42069;
		_caveNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_caveNoise.Frequency = 0.015f;
		_caveNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_caveNoise.FractalOctaves = 5;
		_caveNoise.DomainWarpEnabled = true;
		_caveNoise.DomainWarpAmplitude = 3.0f;

		_caveClusterMask.Seed = mapSeed + 42;
		_caveClusterMask.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_caveClusterMask.Frequency = 0.05f;

		_waterNoise.Seed = mapSeed + 42070;
		_waterNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_waterNoise.Frequency = 0.1f;

		_waterClusterMask.Seed = mapSeed + 43;
		_waterClusterMask.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_waterClusterMask.Frequency = 0.5f;
	}
	
	public float GetSurfaceNoise(int worldX) {
		float noise = (_noise.GetNoise1D(worldX) + 1.0f) / 2.0f;
		_noise.DomainWarpEnabled = true;
	return noise;
	}

	public int GetSurfaceHeight(int worldX) {
	float n = (_noise.GetNoise1D(worldX) + 1.0f) / 2.0f;
	return (int)(n * 30 + 20); 
}

	public string GetBiomeAt(int x, int y) {
		float temp = _tempNoise.GetNoise2D(x, y);
		float moisture = _moistureNoise.GetNoise2D(x, y);
		if (temp > 0.2f) {
			return moisture > 0.2f ? "Dschungel" : "Wüste";
		} else {
			return moisture > 0.2f ? "Tundra" : "Eiswüste";
		}
	}

	public struct OreParams {
		public Vector2I AtlasCoords;
		public float ScaleX;
		public float ScaleY;
		public float Threshold;
		public float ClusterThreshold;
		public int SeedOffset;
	}

	public OreParams GetOreParameters(int x, int y) {
		float depthPerc = (float)y / MapHeight;
		string biome = GetBiomeAt(x, y);
		
		OreParams p = new OreParams {
			AtlasCoords = new Vector2I(7, 0),
			ScaleX = 1.0f,
			ScaleY = 1.0f,
			Threshold = 0.5f,
			ClusterThreshold = 0.2f,
			SeedOffset = 0
		};
		
		float variation = _noise.GetNoise2D(x * 0.1f, y * 0.1f) * 0.05f;
		float modifiedDepth = depthPerc + variation;

		if (biome == "Wüste") {
			p.Threshold += 0.1f;
		}

		if (modifiedDepth > 0.8f) { // Diamant
			p.AtlasCoords = new Vector2I(10, 0);
			p.ScaleX = 2.0f; p.ScaleY = 2.0f;
			p.Threshold = 0.9f;
			p.ClusterThreshold = 0.5f;
			p.SeedOffset = 5000;
		}
		else if (modifiedDepth > 0.55f) { // Gold
			p.AtlasCoords = new Vector2I(9, 0);
			p.ScaleX = 2.5f; p.ScaleY = 0.3f;
			p.Threshold = 0.85f;
			p.ClusterThreshold = 0.4f;
			p.SeedOffset = 2500;
		}
		else if (modifiedDepth > 0.4f) { // Eisen
			p.AtlasCoords = new Vector2I(8, 0);
			p.ScaleX = 0.8f; p.ScaleY = 0.8f;
			p.Threshold = 0.6f;
			p.ClusterThreshold = 0.25f;
			p.SeedOffset = 1000;
		}
		else { // Kohle
			p.AtlasCoords = new Vector2I(7, 0);
			p.ScaleX = 0.2f; p.ScaleY = 3.0f;
			p.Threshold = 0.5f;
			p.ClusterThreshold = 0.15f;
			p.SeedOffset = 0;
		}
		return p;
	}

	public int GetOreOrStone(int x, int y, int surfaceY, string biome) {
		OreParams p = GetOreParameters(x, y);		
		float oreX = x * p.ScaleX;
		float oreY = y * p.ScaleY;
		float veinValue = _oreNoise.GetNoise2D(oreX, oreY);
		float clusterValue = _oreClusterMask.GetNoise2D(x + p.SeedOffset, y);

		bool isOre = veinValue < (1.0f - p.Threshold) && clusterValue > p.ClusterThreshold;

		if (isOre && y > surfaceY + 10) {
			return p.AtlasCoords.X; 
		}
		float depthPerc = (float)y / MapHeight;
		float layerNoise = _noise.GetNoise2D(x * 0.5f, y * 0.5f) * 0.1f;
		float noisyDepth = depthPerc + layerNoise;
		if (noisyDepth < 0.35f) {
		return 1; // Erde (ID 1)
	} 
	else if (noisyDepth < 0.55f) {
		return 4; // Dunkle Erde (ID 4)
	} 
	else if (noisyDepth < 0.85f) {
		return 5; // Stein (ID 5)
	}
	else if (noisyDepth < 1.25f) {
		return 6; // Dunkler Stein (ID 6)
	}  
	else {
		return 3; // Magma (ID 3)
	}
	}

	public int GenerateTile(int x, int y, int surfaceY) {
		if (y < surfaceY) return -1; // -1 = Luft

		string biome = GetBiomeAt(x, y);

		if (IsCave(x, y, biome)) {
			if (ShouldGenerateWater(x, y, surfaceY, biome)) {
				return 2; // 2 = Wasser
			}
			return -1; // Luft
		}

		if (y < surfaceY + 2) return 0; // Gras/Oberfläche

		return GetOreOrStone(x, y, surfaceY, biome);
	}

	public bool IsCave(int x, int y, string biome) {
		float caveValue = _caveNoise.GetNoise2D(x, y);
		float caveClusterValue = _caveClusterMask.GetNoise2D(x, y);
		float threshold = 0.03f;
		
		if (biome == "Dschungel") threshold = 0.08f;
		else if (biome == "Wüste") threshold = 0.02f;

		return Math.Abs(caveValue) < threshold && caveClusterValue > 0.0f;
	}

	public bool ShouldGenerateWater(int x, int y, int surfaceY, string biome) {
		int minDepth = (biome == "Wüste") ? 100 : 20;
		if (y < surfaceY + minDepth) return false;

		float waterValue = _waterNoise.GetNoise2D(x, y);
		float wMask = _waterClusterMask.GetNoise2D(x, y);
		
		float relativeDepth = (float)(y - surfaceY) / (MapHeight - surfaceY);
		float noiseInfluence = 0.5f;
		float biomeBonus = (biome == "Dschungel") ? 0.2f : 0.0f;
		
		float finalValue = relativeDepth + (waterValue * noiseInfluence) - biomeBonus;

		return Math.Abs(finalValue) < 0.75f && wMask > 0.0f;
	}
}
