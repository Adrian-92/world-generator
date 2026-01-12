using Godot;
using System.Collections.Generic;

public static class GlobalConstants  {
public enum TileType : byte { AIR, STATIC, BEDROCK, LIQUID, FALLING }

public struct TileProperty {
	public TileType Type;
	public Vector2I AtlasCoords;
	public int AtlasID;
	public float Viscosity;
}
	
}
