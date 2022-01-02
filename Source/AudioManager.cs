using System.Threading;
using AudioSwitcher.AudioApi.CoreAudio;

public class AudioManager {
	public double Volume {
		get {
			if (device == null)
				return 0.0;
			return device.Volume;
		}
		set {
			if (device == null)
				return;
			device.SetVolumeAsync(value);
		}
	}

	CoreAudioDevice device;

	void Initialize() {
		device = new CoreAudioController().DefaultPlaybackDevice;

	}

	public AudioManager() {
		var thread = new Thread(Initialize);
		thread.Start();
	}
}