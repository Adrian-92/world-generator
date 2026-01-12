using Godot;
using System;
using System.Collections.Generic;
using static BiomeData;
using static GlobalConstants;
public partial class WorldGenerator : Node
{

	[Export] public int MapHeight = 350;
	[Export] public int StartingAreaSize = 128;
	private FastNoiseLite _tempNoise = new FastNoiseLite();
	private FastNoiseLite _moistureNoise = new FastNoiseLite();

	private FastNoiseLite _noise = new FastNoiseLite();
	private FastNoiseLite _oreNoise = new FastNoiseLite();
	private FastNoiseLite _oreClusterMask = new FastNoiseLite();
	private FastNoiseLite _caveNoise = new FastNoiseLite();
	private FastNoiseLite _caveClusterMask = new FastNoiseLite();
	private FastNoiseLite _waterNoise = new FastNoiseLite();
	private FastNoiseLite _waterClusterMask = new FastNoiseLite();
	
	public struct TileContext {
		public Biome Biome;
		public float Depth;
		public int SurfaceY;
		public BiomeParams BiomeParams;
	}
	
	
	public void SetupNoise(int mapSeed, float noiseFrequency) {
		_tempNoise.Seed = mapSeed;
		_tempNoise.Frequency = 0.0005f;
		_moistureNoise.Seed = mapSeed + 123;
		_moistureNoise.Frequency = 0.0005f;

		_noise.Seed = mapSeed;
		_noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noise.Frequency = noiseFrequency;
		_noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noise.FractalOctaves = 2;
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
		// Weniger Frequenz = Längere, weitere Tunnel
		_caveNoise.Frequency = 0.01f; 
		_caveNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_caveNoise.FractalOctaves = 2; // Weniger Oktaven machen die Wände glatter

		_caveNoise.DomainWarpEnabled = true;
		_caveNoise.DomainWarpAmplitude = 30.0f;
		_caveNoise.DomainWarpFrequency = 0.02f;

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
		return (int)(n * 30); 
	}

	public Biome GetBiomeAt(int x, int y) {
		float boundaryWarp = _noise.GetNoise2D(x * 0.2f, y * 0.2f) * 24.0f;
		float warpedX = x + boundaryWarp;
		float depthWarp = _noise.GetNoise2D(x * 0.1f, y * 0.1f) * 10.0f;
		if (y + depthWarp > MapHeight * 0.8f) return Biome.LAVA;
		if (Math.Abs(warpedX) < StartingAreaSize) return Biome.FOREST;
		float temp = _tempNoise.GetNoise2D(x, y * 0.25f);
		float moisture = _moistureNoise.GetNoise2D(x, y * 0.25f);
		if (temp > 0.2f) {
			return moisture > 0.0f ? Biome.FOREST : Biome.DESERT;
		} else {
			return moisture > 0.0f ? Biome.TUNDRA : Biome.ICE;
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

	public OreParams GetOreParameters(int x, int y, TileContext ctx) {
		float depthPerc = (float)y / MapHeight;
		float biomeFactor = ctx.BiomeParams.OreParamThreshold;
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
		
		if (modifiedDepth > 0.8f) { // Diamant
			p.AtlasCoords = new Vector2I(11, 0);
			p.ScaleX = 2.0f; p.ScaleY = 2.0f;
			p.Threshold = 0.9f;
			p.ClusterThreshold = 0.5f;
			p.SeedOffset = 5000;
		}
		else if (modifiedDepth > 0.55f) { // Gold
			p.AtlasCoords = new Vector2I(10, 0);
			p.ScaleX = 2.5f; p.ScaleY = 0.3f;
			p.Threshold = 0.85f;
			p.ClusterThreshold = 0.4f;
			p.SeedOffset = 2500;
		}
		else if (modifiedDepth > 0.4f) { // Eisen
			p.AtlasCoords = new Vector2I(9, 0);
			p.ScaleX = 0.8f; p.ScaleY = 0.8f;
			p.Threshold = 0.6f;
			p.ClusterThreshold = 0.25f;
			p.SeedOffset = 1000;
		}
		else { // Kohle
			p.AtlasCoords = new Vector2I(8, 0);
			p.ScaleX = 0.1f; p.ScaleY = 2.5f;
			p.Threshold = 0.5f;
			p.ClusterThreshold = 0.25f;
			p.SeedOffset = 0;
		}
		
		p.Threshold += biomeFactor;
		
		return p;
	}

	public Tile GetOreOrStone(int x, int y, TileContext ctx) {
		OreParams p = GetOreParameters(x, y, ctx);		
		Tile generatedTile = new Tile();
		generatedTile.Type = TileType.STATIC;
		float oreX = x * p.ScaleX;
		float oreY = y * p.ScaleY;
		float veinValue = _oreNoise.GetNoise2D(oreX, oreY);
		float clusterValue = _oreClusterMask.GetNoise2D(x + p.SeedOffset, y);

		bool isOre = veinValue < (1.0f - p.Threshold) && clusterValue > p.ClusterThreshold;

		if (isOre && y > ctx.SurfaceY + 10) {
			generatedTile.TileID = (short) p.AtlasCoords.X;
			generatedTile.Health = 80;
			generatedTile.Type = TileType.STATIC;
			return generatedTile; 
		}
		float depthPerc = (float) y / MapHeight;
		float layerNoise = _noise.GetNoise2D(x * 0.5f, y * 0.5f) * 0.1f;
		float layerWarp = _noise.GetNoise2D(x * 0.05f, 0) * 0.05f; 
		float noisyDepth = depthPerc + layerNoise + layerWarp;
		

		if (noisyDepth < 0.25f) {
			generatedTile.Health = 10;
			generatedTile.TileID = 1; // Erde
		} 
		else if (noisyDepth < 0.55f) {
			generatedTile.Health = 10;
			generatedTile.TileID = 5; // Dunkle Erde 
		} 
		else if (noisyDepth < 0.75f) {
			generatedTile.Health = 50;
			generatedTile.TileID = 6; // Stein
		}
		else if (noisyDepth < 0.9f) {
			generatedTile.Health = 50;
			generatedTile.TileID = 7; // Dunkler Stein
		}  
		else {
			generatedTile.Damage = 100;
			generatedTile.TileID = 4; // Magma
		}
		return generatedTile;
	}
	
	public Tile GenerateTile(int x, int y, int surfaceY) {
		Tile generatedTile = new Tile();
		
		if (y < surfaceY) {
			generatedTile.Type = TileType.AIR;
			generatedTile.TileID = 0;
			return generatedTile;
		}
		BiomeData.Biome biomeType = GetBiomeAt(x, y);
		var bParams = BiomeData.GetParams(biomeType);
		TileContext ctx = new TileContext {
			SurfaceY = surfaceY,
			Depth = Mathf.Clamp((float)(y - surfaceY) / (MapHeight - surfaceY), 0.0f, 1.0f),
			Biome = biomeType,
			BiomeParams = bParams
		};

		int bedrockLayer = MapHeight - 5;
		if (y >= bedrockLayer) {
			float n = _noise.GetNoise2D(x * 0.5f, y * 0.5f);
			if (y >= MapHeight - 1 || n > 0.0f) {
				generatedTile.Type = TileType.BEDROCK;
				generatedTile.Health = -1;
				generatedTile.TileID = 12;
				return generatedTile;
			}
		}

		bool isCave = IsCave(x, y, ctx);
		if (isCave) {
			generatedTile.HasBackground = true;
			bool isCaveBelow = IsCave(x, y + 1, ctx);
			
			if (ctx.Depth < 0.7f) {
				if (ShouldGenerateWater(x, y, ctx)) {
					generatedTile.Type = TileType.LIQUID;
					generatedTile.Viscosity = 1.0f;
					generatedTile.TileID = 3;
					return generatedTile;
				}
			} else {
				if (ShouldGenerateLava(x, y, ctx)) {
					if (!isCaveBelow || _waterClusterMask.GetNoise2D(x + 1000, y + 1000) > 0.6f) {
						generatedTile.Type = TileType.LIQUID;
						generatedTile.Viscosity = 0.2f;
						generatedTile.TileID = 4;
						return generatedTile;
					}
				}
			}
			generatedTile.Type = TileType.AIR;
			generatedTile.TileID = 0;
			return generatedTile;
		}

		if (y == surfaceY) {
			generatedTile.Type = TileType.STATIC;
			generatedTile.TileID = 2; // Gras
			generatedTile.Health = 10;
			return generatedTile;
		}

		return GetOreOrStone(x, y, ctx);
	}
	
	public bool GenerateBackgroundTile(int x, int y, int surfaceY) {
		TileContext ctx = new TileContext {
		SurfaceY = surfaceY,
		Depth = Mathf.Clamp((float)(y - surfaceY) / (MapHeight - surfaceY), 0.0f, 1.0f),
		Biome = GetBiomeAt(x, y) 
		};
		if (IsCave(x, y, ctx)){
			return true;
		}
		return false;
	}
	
	public bool IsCave(int x, int y, TileContext ctx) {
		float caveValue;
		
		if(ctx.Biome == Biome.LAVA) caveValue = _caveNoise.GetNoise2D(x * 0.5f, y);
		else caveValue = _caveNoise.GetNoise2D(x, y);
		
		return Math.Abs(caveValue) < ctx.BiomeParams.CaveThreshold;
		}

	public bool ShouldGenerateLava(int x, int y, TileContext ctx) {
		if(ctx.Biome != Biome.LAVA) return false;
		float lMask = _waterClusterMask.GetNoise2D(x + 1000, y + 1000); 
		return lMask + (ctx.Depth * 0.5f) > 0.0f;
	}
	public bool ShouldGenerateWater(int x, int y, TileContext ctx) {
		if(ctx.Biome == Biome.LAVA) return false;
		int minDepth = ctx.BiomeParams.WaterDepth;
		float biomeBonus = ctx.BiomeParams.WaterFactor;
		if (y < ctx.SurfaceY + minDepth) return false;

		float waterValue = _waterNoise.GetNoise2D(x, y);
		float wMask = _waterClusterMask.GetNoise2D(x, y);
		
		float depthFactor = Math.Abs((float)(y - ctx.SurfaceY) / MapHeight);
		float finalValue = depthFactor * waterValue - biomeBonus;
		float wMaskValue = wMask + depthFactor;
		return finalValue < 0.8f && wMaskValue > 0.5f;
	}
}
