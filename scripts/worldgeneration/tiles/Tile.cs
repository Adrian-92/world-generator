using Godot;
using System;
using static GlobalConstants;
public struct Tile 
{
	public short TileID;      
	public short Health;      
	public TileType Type;     
	public byte ViscosityInt; 
	public short Damage;      
	public bool HasBackground;

	public float Viscosity {
		get => ViscosityInt / 255f;
		set => ViscosityInt = (byte)(Mathf.Clamp(value, 0, 1) * 255);
	}

	public static Tile CreateAir() {
		return new Tile {
			TileID = 0,
			Type = TileType.AIR,
			Health = 0
		};
	}

	public Tile(short tileID, short health, TileType type, float viscosity, short damage) {
		this.TileID = tileID;
		this.Health = health;
		this.Type = type;
		this.Damage = damage;
		this.HasBackground = false;
		// Viskosität intern als Byte speichern
		this.ViscosityInt = (byte)(Mathf.Clamp(viscosity, 0, 1) * 255);
	}
}
