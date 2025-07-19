using Godot;

public partial class AreaMusic : Area2D
{
	[Export] public AudioStream MyMusicStream;
	[Export] public float FadeDuration = 1.0f; // Thời gian fade (giây)
	[Export] public float MaxVolume = 0.0f; // Volume tối đa (dB)
	[Export] public float MinVolume = -80.0f; // Volume tối thiểu (dB)
	
	private AudioStreamPlayer bgmPlayer;
	private Tween currentTween;
	private bool isPlayerInside = false;

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
				GD.Print("WAV looping may require external configuration or custom handling.");
			}
			GD.Print("Audio stream assigned successfully with looping enabled.");
		}
		else
		{
			GD.PrintErr("MyMusicStream not assigned!");
		}

		// Đặt volume ban đầu ở mức tối thiểu
		bgmPlayer.VolumeDb = MinVolume;
		
		GD.Print($"Area2D Parent: {GetParent()?.Name}, Monitoring: {Monitoring}, Monitorable: {Monitorable}");
	}

	public void _onBodyEntered(Node body)
	{
		GD.Print($"Entered Area by {body?.Name}");
		if (body?.IsInGroup("Player") == true)
		{
			isPlayerInside = true;
			
			// Bắt đầu phát nhạc nếu chưa phát
			if (!bgmPlayer.Playing)
			{
				bgmPlayer.Play();
			}
			
			// Fade in - tăng dần volume
			FadeIn();
			GD.Print("Fading in audio.");
		}
	}

	public void _onBodyExited(Node body)
	{
		GD.Print($"Exited Area by {body?.Name}");
		if (body?.IsInGroup("Player") == true)
		{
			isPlayerInside = false;
			
			// Fade out - giảm dần volume
			FadeOut();
			GD.Print("Fading out audio.");
		}
	}

	private void FadeIn()
	{
		// Hủy tween cũ nếu có
		currentTween?.Kill();
		
		// Tạo tween mới cho fade in
		currentTween = CreateTween();
		
		// Tween volume từ volume hiện tại đến MaxVolume
		currentTween.TweenProperty(bgmPlayer, "volume_db", MaxVolume, FadeDuration);
	}

	private void FadeOut()
	{
		// Hủy tween cũ nếu có
		currentTween?.Kill();
		
		// Tạo tween mới cho fade out
		currentTween = CreateTween();
		
		// Tween volume từ volume hiện tại về MinVolume
		currentTween.TweenProperty(bgmPlayer, "volume_db", MinVolume, FadeDuration);
		
		// Chỉ dừng nhạc nếu player thực sự không còn trong vùng
		currentTween.TweenCallback(Callable.From(() => {
			if (!isPlayerInside && bgmPlayer.Playing)
			{
				bgmPlayer.Stop();
				GD.Print("Audio stopped after fade out.");
			}
		}));
	}
}
