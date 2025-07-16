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
			// Enable looping for supported audio streams
			if (MyMusicStream is AudioStreamMP3 mp3Stream)
			{
				mp3Stream.Loop = true;
			}
			else if (MyMusicStream is AudioStreamOggVorbis oggStream)
			{
				oggStream.Loop = true;
			}
			// WAV looping is handled differently in Godot 4
			else if (MyMusicStream.GetClass() == "AudioStreamWAV")
			{
				// Godot 4 WAV looping requires setting loop points or using external tools
				GD.Print("WAV looping may require external configuration or custom handling.");
			}
			GD.Print("Audio stream assigned successfully with looping enabled.");
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
