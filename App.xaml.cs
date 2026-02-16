using System.Configuration;
using System.Data;
using System;
using System.IO;
using System.Windows.Media;
using System.Windows;
using System.Threading.Tasks;
using System.Linq;
using System.Media;
using System.Security.Cryptography;
using System.Text;

namespace Launcher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
	private readonly MediaPlayer _buttonPlayer = new();
	private bool _buttonSoundReady;
	private bool _buttonSoundOpening;
	private bool _buttonSoundDisabled;
	public static double UiSfxVolume { get; private set; } = 0.6;
	private static readonly byte[] AssetKey = SHA256.HashData(Encoding.UTF8.GetBytes("LauncherAssetsKey_v1"));

	public App()
	{
		TryLogAsset("App started.");
		TryGenerateEncryptedAssetsIfNeeded();
		DispatcherUnhandledException += (_, e) =>
		{
			LogCrash(e.Exception);
			e.Handled = true;
			System.Windows.MessageBox.Show("Произошла ошибка запуска. Подробности записаны в crash.log.", "Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
		};

		AppDomain.CurrentDomain.UnhandledException += (_, e) =>
		{
			if (e.ExceptionObject is Exception ex)
			{
				LogCrash(ex);
			}
		};

		TaskScheduler.UnobservedTaskException += (_, e) =>
		{
			LogCrash(e.Exception);
			e.SetObserved();
		};
	}

	public static void SetUiSfxVolume(double value)
	{
		UiSfxVolume = Math.Clamp(value, 0, 1);
	}

	private void OnButtonMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
	{
		PlayButtonSound();
	}

	private void OnButtonPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
	{
		PlayButtonSound();
	}

	private void PlayButtonSound()
	{
		if (_buttonSoundDisabled)
		{
			SystemSounds.Asterisk.Play();
			return;
		}

		if (!_buttonSoundReady)
		{
			TryOpenButtonSound();
			return;
		}

		_buttonPlayer.Position = TimeSpan.Zero;
		_buttonPlayer.Volume = UiSfxVolume;
		_buttonPlayer.Play();
	}

	private void TryOpenButtonSound()
	{
		if (_buttonSoundOpening)
		{
			return;
		}

		var soundPath = EnsurePlayableAssetFile("buttons.mp3");
		if (string.IsNullOrWhiteSpace(soundPath) || !File.Exists(soundPath))
		{
			TryLogAsset("Button sound file not found.");
			return;
		}

		_buttonSoundOpening = true;
		_buttonPlayer.MediaOpened += OnButtonSoundOpened;
		_buttonPlayer.MediaFailed += OnButtonSoundFailed;
		_buttonPlayer.Open(new Uri(soundPath, UriKind.Absolute));
	}

	private void OnButtonSoundOpened(object? sender, EventArgs e)
	{
		_buttonPlayer.MediaOpened -= OnButtonSoundOpened;
		_buttonSoundReady = true;
		_buttonSoundOpening = false;
		_buttonPlayer.Position = TimeSpan.Zero;
		_buttonPlayer.Volume = UiSfxVolume;
		_buttonPlayer.Play();
		TryLogAsset("Button sound opened.");
	}

	private void OnButtonSoundFailed(object? sender, ExceptionEventArgs e)
	{
		_buttonPlayer.MediaOpened -= OnButtonSoundOpened;
		_buttonSoundOpening = false;
		_buttonSoundDisabled = true;
		SystemSounds.Asterisk.Play();
		TryLogAsset($"Button sound failed: {e.ErrorException?.Message}");
	}

	public void PlayUiSound()
	{
		PlayButtonSound();
	}

	public static string? EnsurePlayableAssetFile(string fileName)
	{
		try
		{
			var encryptedPath = FindEncryptedAsset(fileName);
			if (!string.IsNullOrWhiteSpace(encryptedPath))
			{
				var decrypted = EnsureDecryptedAsset(encryptedPath, fileName);
				if (!string.IsNullOrWhiteSpace(decrypted) && File.Exists(decrypted))
				{
					return decrypted;
				}
				TryLogAsset($"Decryption failed for {encryptedPath}, falling back to raw assets.");
			}

			var baseDir = AppDomain.CurrentDomain.BaseDirectory;
			var plainPath = Path.Combine(baseDir, "Assets", fileName);
			if (File.Exists(plainPath))
			{
				TryLogAsset($"Using plain asset: {plainPath}");
				return plainPath;
			}

			var rawEncryptedDir = Path.Combine(baseDir, "AssetsEncrypted");
			var rawEncryptedPath = Path.Combine(rawEncryptedDir, fileName);
			if (File.Exists(rawEncryptedPath))
			{
				TryLogAsset($"Using raw asset from AssetsEncrypted: {rawEncryptedPath}");
				return rawEncryptedPath;
			}

			var localRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launcher");
			var localRawPath = Path.Combine(localRoot, "AssetsEncrypted", fileName);
			if (File.Exists(localRawPath))
			{
				TryLogAsset($"Using raw asset from LocalAppData: {localRawPath}");
				return localRawPath;
			}

			TryLogAsset($"Asset not found: {fileName}");
			return null;
		}
		catch
		{
			TryLogAsset($"Asset extraction failed for {fileName}");
			return null;
		}
	}

	private static string? FindEncryptedAsset(string fileName)
	{
		var baseDirs = new[]
		{
			AppContext.BaseDirectory,
			AppDomain.CurrentDomain.BaseDirectory,
			Environment.CurrentDirectory
		};

		foreach (var dir in baseDirs.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			if (string.IsNullOrWhiteSpace(dir))
			{
				continue;
			}

			var basePath = Path.Combine(dir, "AssetsEncrypted", $"{fileName}.enc");
			if (File.Exists(basePath))
			{
				TryLogAsset($"Found encrypted asset: {basePath}");
				return basePath;
			}
		}

		var localRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launcher");
		var localPath = Path.Combine(localRoot, "AssetsEncrypted", $"{fileName}.enc");
		if (File.Exists(localPath))
		{
			TryLogAsset($"Found encrypted asset in LocalAppData: {localPath}");
			return localPath;
		}

		TryLogAsset($"Encrypted asset not found for {fileName}. Probed dirs: {string.Join("; ", baseDirs)}");
		return null;
	}

	private static string? EnsureDecryptedAsset(string encryptedPath, string fileName)
	{
		var localRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launcher");
		var cacheDir = Path.Combine(localRoot, "AssetsCache");
		Directory.CreateDirectory(cacheDir);
		var cachePath = Path.Combine(cacheDir, fileName);

		if (File.Exists(cachePath))
		{
			var cacheTime = File.GetLastWriteTimeUtc(cachePath);
			var encTime = File.GetLastWriteTimeUtc(encryptedPath);
			if (cacheTime >= encTime)
			{
				TryLogAsset($"Using cached asset: {cachePath}");
				return cachePath;
			}
		}

		try
		{
			using var input = File.OpenRead(encryptedPath);
			var iv = new byte[16];
			if (input.Read(iv, 0, iv.Length) != iv.Length)
			{
				TryLogAsset($"Invalid encrypted asset: {encryptedPath}");
				return null;
			}

			using var aes = Aes.Create();
			aes.Key = AssetKey;
			aes.IV = iv;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;

			using var decryptor = aes.CreateDecryptor();
			using var crypto = new CryptoStream(input, decryptor, CryptoStreamMode.Read);
			using var output = File.Create(cachePath);
			crypto.CopyTo(output);
			TryLogAsset($"Decrypted asset -> {cachePath}");
			return cachePath;
		}
		catch
		{
			TryLogAsset($"Failed to decrypt asset: {encryptedPath}");
			return null;
		}
	}

	private static void TryGenerateEncryptedAssetsIfNeeded()
	{
		try
		{
			var baseDir = AppContext.BaseDirectory;
			var plainDir = Path.Combine(baseDir, "Assets");
			var encryptedDir = Path.Combine(baseDir, "AssetsEncrypted");
			Directory.CreateDirectory(encryptedDir);
			if (Directory.Exists(plainDir))
			{
				EncryptFileIfNeeded(Path.Combine(plainDir, "back.mp4"), Path.Combine(encryptedDir, "back.mp4.enc"));
				EncryptFileIfNeeded(Path.Combine(plainDir, "backg.mp4"), Path.Combine(encryptedDir, "backg.mp4.enc"));
				EncryptFileIfNeeded(Path.Combine(plainDir, "buttons.mp3"), Path.Combine(encryptedDir, "buttons.mp3.enc"));
			}

			EncryptFileIfNeeded(Path.Combine(encryptedDir, "back.mp4"), Path.Combine(encryptedDir, "back.mp4.enc"));
			EncryptFileIfNeeded(Path.Combine(encryptedDir, "backg.mp4"), Path.Combine(encryptedDir, "backg.mp4.enc"));
			EncryptFileIfNeeded(Path.Combine(encryptedDir, "buttons.mp3"), Path.Combine(encryptedDir, "buttons.mp3.enc"));
		}
		catch
		{
			// Ignore encryption failures.
		}
	}

	private static void EncryptFileIfNeeded(string inputPath, string outputPath)
	{
		if (!File.Exists(inputPath) || File.Exists(outputPath))
		{
			return;
		}

		using var aes = Aes.Create();
		aes.Key = AssetKey;
		aes.GenerateIV();
		aes.Mode = CipherMode.CBC;
		aes.Padding = PaddingMode.PKCS7;

		using var input = File.OpenRead(inputPath);
		using var output = File.Create(outputPath);
		output.Write(aes.IV, 0, aes.IV.Length);
		using var encryptor = aes.CreateEncryptor();
		using var crypto = new CryptoStream(output, encryptor, CryptoStreamMode.Write);
		input.CopyTo(crypto);
		TryLogAsset($"Encrypted asset: {outputPath}");
	}

	private static void TryLogAsset(string message)
	{
		try
		{
			var localRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launcher");
			Directory.CreateDirectory(localRoot);
			var logPath = Path.Combine(localRoot, "asset.log");
			File.AppendAllText(logPath, $"[{DateTime.Now:O}] {message}\n");
			return;
		}
		catch
		{
			// Ignore logging failures.
		}
	}

	private static void LogCrash(Exception ex)
	{
		try
		{
			var baseDir = AppDomain.CurrentDomain.BaseDirectory;
			var logPath = Path.Combine(baseDir, "crash.log");
			File.AppendAllText(logPath, $"[{DateTime.Now:O}] {ex}\n\n");
		}
		catch
		{
			// Ignore logging failures.
		}
	}
}

