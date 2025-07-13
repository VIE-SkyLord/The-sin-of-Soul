using Godot;

public partial class Enermy : CharacterBody2D
{
	[Export] public float Speed = 500.0f;
	[Export] public float JumpVelocity = -700.0f;
	[Export] public float Gravity = 980.0f;
	
	private bool isPossessing = false;

	private bool isPossessed = false; // Flag to track possession state

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		// GRAVITY ALWAYS APPLIES - regardless of possession state
		if (!IsOnFloor())
			velocity.Y += Gravity * (float)delta;

		// Handle jump and movement only when possessed
		if (isPossessed)
		{
			// Handle jump - changed to IsActionPressed for hold-to-jump
			if (Input.IsActionPressed("move_up") && IsOnFloor())
			{
				velocity.Y = JumpVelocity;
				GD.Print("Enemy jump input detected");
			}

			// Handle movement
			float direction = Input.GetAxis("move_left", "move_right");
			if (direction != 0)
			{
				velocity.X = direction * Speed;
				GD.Print($"Enemy moving with direction: {direction}");
			}
			else
			{
				velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
			}
		}
		else
		{
			// When not possessed, gradually stop horizontal movement
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed * (float)delta * 2);
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	// Method to set possession state (called by Player)
	public void SetPossessed(bool possessed)
	{
		isPossessed = possessed;
		GD.Print($"Enemy possession state set to: {isPossessed}");
	}
}
