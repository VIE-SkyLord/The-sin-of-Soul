using Godot;

public partial class AreaMusic : Area2D
{
	[Export] public AudioStream MyMusicStream;
	private AudioStreamPlayer bgmPlayer;

	public override void _Ready()
	{
		bgmPlayer = GetNode<AudioStreamPlayer>("StartArc1");
		if (bgmPlayer == null)
		{
			GD.PrintErr("bgmPlayer not found!");
			return;
		}
		if (MyMusicStream != null)
		{
			bgmPlayer.Stream = MyMusicStream;
			GD.Print("Audio stream assigned successfully.");
		}
		else
		{
			GD.PrintErr("MyMusicStream not assigned!");
		}
		GD.Print($"Area2D Parent: {GetParent()?.Name}, Monitoring: {Monitoring}, Monitorable: {Monitorable}");
	}

	public void _onBodyEntered(Node body)
	{
		GD.Print($"Entered Area by {body?.Name}");
		if (body?.IsInGroup("Player") == true && !bgmPlayer.Playing)
		{
			bgmPlayer.Play();
			GD.Print("Playing audio.");
		}
	}

	public void _onBodyExited(Node body)
	{
		GD.Print($"Exited Area by {body?.Name}");
		if (body?.IsInGroup("Player") == true && bgmPlayer.Playing)
		{
			bgmPlayer.Stop();
			GD.Print("Stopped audio.");
		}
	}
}
