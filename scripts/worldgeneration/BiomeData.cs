using Godot;
using System;
using System.Collections.Generic;

public static class BiomeData 
{
	public enum Biome {
		FOREST,
		DESERT,
		ICE,
		TUNDRA,
		CAVE,
		LAVA
	}

	public struct BiomeParams {
		public int AtlasID;
		public float OreGenFactor;
		public float OreParamThreshold;
		public float CaveThreshold; 
		public int WaterDepth;
		public float WaterFactor;
	}

	private static readonly Dictionary<Biome, BiomeParams> _data = new Dictionary<Biome, BiomeParams>();

	static BiomeData() {
		_data[Biome.FOREST] = new BiomeParams {
			AtlasID = 1,
			OreGenFactor = 0.0f,
			OreParamThreshold = -0.1f,
			CaveThreshold = 0.01f,
			WaterDepth = 0,
			WaterFactor = 0.0f
		};

		_data[Biome.DESERT] = new BiomeParams {
			AtlasID = 1,
			OreGenFactor = 0.0f,
			OreParamThreshold = 0.2f,
			CaveThreshold = 0.005f,
			WaterDepth = 100,
			WaterFactor = -0.2f
		};

		_data[Biome.ICE] = new BiomeParams {
			AtlasID = 1,
			OreGenFactor = 0.0f,
			OreParamThreshold = 0.3f,
			CaveThreshold = 0.015f,
			WaterDepth = 10,
			WaterFactor = 0.0f
		};

		_data[Biome.TUNDRA] = new BiomeParams {
			AtlasID = 1,
			OreGenFactor = 0.0f,
			OreParamThreshold = -0.2f,
			CaveThreshold = 0.02f,
			WaterDepth = 0,
			WaterFactor = 0.1f
		};

		_data[Biome.LAVA] = new BiomeParams {
			AtlasID = 1,
			OreGenFactor = 0.0f,
			OreParamThreshold = -1.0f,
			CaveThreshold = 0.1f,
			WaterDepth = 0,
			WaterFactor = 0.0f
		};
		
		_data[Biome.CAVE] = new BiomeParams {
			AtlasID = 1,
			OreGenFactor = 0.0f,
			OreParamThreshold = -1.0f,
			CaveThreshold = 0.03f,
			WaterDepth = 0,
			WaterFactor = 0.0f
		};
	}

	public static BiomeParams GetParams(Biome biome) {
		return _data[biome];
	}
}
