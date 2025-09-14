using Godot;
using System;

public partial class Sprite2dGodot : Sprite2D
{
	//print Hello World
	public override void _Ready()
	{
		GD.Print("Hello World");
	}	
	private int _speed = 400;
	private float _angularSpeed = Mathf.Pi;

	public override void _Process(double delta)
	{
		Rotation += _angularSpeed * (float)delta;
		var velocity = Vector2.Up.Rotated(Rotation) * _speed;

		Position += velocity * (float)delta;
	}
}
