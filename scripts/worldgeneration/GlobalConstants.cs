using Godot;
using System.Collections.Generic;

public static class GlobalConstants  {
	public enum Biome {
		FOREST,
		DESERT,
		ICE,
		TUNDRA,
		CAVE
	}
	
	public enum TileType {
		AIR,
		STATIC,
		LIQUID, 
		FALLING
		}
		
	public struct TileProperty {
	public Vector2I AtlasCoords;
	public TileType Type;
	public float Viscosity; 
	public int Damage;      
}
	
}
