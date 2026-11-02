namespace MusicStreamingService;

// Учебная модель музыкального стримингового сервиса по UML-диаграмме.

public interface ITrackSource
{
    Track[] GetTracks();
}

public sealed class User
{
    private readonly List<Playlist> _playlists = new();

    public string UserId { get; }
    public string Login { get; set; }
    public string Email { get; set; }
    public List<Subscription> Subscriptions { get; } = new();
    public List<Listening> Listenings { get; } = new();

    public User(string userId, string login, string email)
    {
        UserId = userId;
        Login = login;
        Email = email;
    }

    public void AddPlaylist(Playlist playlist)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        if (playlist.Owner != this)
            throw new InvalidOperationException("Плейлист должен принадлежать этому пользователю.");
        if (!_playlists.Contains(playlist)) _playlists.Add(playlist);
    }

    public bool HasAccess(Playlist playlist) => playlist.IsAccessibleBy(UserId);
    public IReadOnlyList<Playlist> Playlists => _playlists.AsReadOnly();
}

public sealed class Playlist : ITrackSource
{
    private readonly List<Track> _tracks = new();
    public string PlaylistId { get; }
    public string Name { get; set; }
    public bool IsPublic { get; set; }
    public User Owner { get; }

    public Playlist(User owner, string name, bool isPublic)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        PlaylistId = Guid.NewGuid().ToString("N");
        Name = name;
        IsPublic = isPublic;
        Owner.AddPlaylist(this);
    }

    public Track[] GetTracks() => _tracks.ToArray();
    public bool IsAccessibleBy(string userId) => IsPublic || Owner.UserId == userId;
    public void AddTrack(Track track) { ArgumentNullException.ThrowIfNull(track); if (!_tracks.Contains(track)) _tracks.Add(track); }
    public bool RemoveTrack(Track track) => _tracks.Remove(track);
}

public sealed class RecommendedCollection : ITrackSource
{
    private readonly List<Track> _tracks = new();
    public string CollectionId { get; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; }
    public RecommendedCollection(string name) => Name = name;
    public Track[] GetTracks() => _tracks.ToArray();
    public void AddTrack(Track track) { ArgumentNullException.ThrowIfNull(track); if (!_tracks.Contains(track)) _tracks.Add(track); }
}

public sealed class Track
{
    public string TrackId { get; }
    public string Name { get; set; }
    public string Genre { get; set; }
    private readonly byte[] _audioFile;

    public Track(string trackId, string name, string genre, byte[]? audioFile = null)
    {
        TrackId = trackId;
        Name = name;
        Genre = genre;
        _audioFile = audioFile is null ? Array.Empty<byte>() : (byte[])audioFile.Clone();
    }
    public byte[] GetAudioFile() => (byte[])_audioFile.Clone();
}

public sealed class Subscription
{
    public DateTime StartDate { get; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; }
    public decimal MonthlyPrice { get; set; }
    public Subscription(DateTime startDate, DateTime endDate, decimal monthlyPrice, string status = "active")
    { StartDate = startDate; EndDate = endDate; MonthlyPrice = monthlyPrice; Status = status; }
    public bool IsActive() => Status.Equals("active", StringComparison.OrdinalIgnoreCase)
        && StartDate <= DateTime.Now && EndDate >= DateTime.Now;
}

public sealed class Listening
{
    public DateTime Timestamp { get; }
    public int Duration { get; }
    public Track Track { get; }
    public Listening(Track track, int duration, DateTime? timestamp = null)
    { Track = track; Duration = duration; Timestamp = timestamp ?? DateTime.Now; }
    public void Save() => Console.WriteLine($"Прослушивание сохранено: {Track.Name}, {Duration} сек.");
}

public sealed class PlaylistService
{
    private readonly Dictionary<string, User> _users = new();
    public void RegisterUser(User user) => _users[user.UserId] = user;
    public User? FindUser(string userId) => _users.GetValueOrDefault(userId);
    public Playlist CreatePlaylist(string userId, string name, bool isPublic)
    {
        var owner = FindUser(userId) ?? throw new ArgumentException("Пользователь не найден.", nameof(userId));
        return new Playlist(owner, name, isPublic);
    }
}

public sealed class PlaylistEditor
{
    private readonly PlaylistService _service;
    public PlaylistEditor(PlaylistService service) => _service = service;
    public Playlist Submit(string userId, string name, bool isPublic) => _service.CreatePlaylist(userId, name, isPublic);
}

public sealed class AudioOutput
{
    private byte[] _source = Array.Empty<byte>();
    public void SetSource(byte[] audio) => _source = audio is null ? Array.Empty<byte>() : (byte[])audio.Clone();
    public void Play(byte[] audio)
    {
        SetSource(audio);
        Console.WriteLine($"Воспроизведение имитируется: {_source.Length} байт аудио.");
    }
}

public sealed class Player
{
    private readonly AudioOutput _output;
    public Track? CurrentTrack { get; private set; }
    public Player(AudioOutput output) => _output = output;
    public void StartPlayback(Track[] tracks) { foreach (var track in tracks) PlayNextTrack(track); }
    public void PlayNextTrack(Track track)
    {
        CurrentTrack = track;
        PrepareAudio(track);
    }
    public byte[] PrepareAudio(Track track)
    {
        var audio = track.GetAudioFile();
        _output.SetSource(audio);
        _output.Play(audio);
        return audio;
    }
}

public sealed class PlaybackController
{
    private readonly Player _player;
    public PlaybackController(Player player) => _player = player;

    public string PlayPlaylist(string playlistId, string userId, IEnumerable<Playlist> playlists, IEnumerable<Subscription> subscriptions)
    {
        if (!subscriptions.Any(s => s.IsActive())) return "Subscription inactive";
        var playlist = playlists.FirstOrDefault(p => p.PlaylistId == playlistId);
        if (playlist is null) return "Playlist not found";
        if (!playlist.IsAccessibleBy(userId)) return "Access denied: private playlist";
        _player.StartPlayback(playlist.GetTracks());
        return "Playback completed";
    }

    public void PlayNextTrack(Track track) => _player.PlayNextTrack(track);
}

public static class Program
{
    public static void Main()
    {
        var user = new User("u-001", "alex", "alex@example.com");
        var subscription = new Subscription(DateTime.Today, DateTime.Today.AddMonths(1), 299m);
        user.Subscriptions.Add(subscription);
        var service = new PlaylistService();
        service.RegisterUser(user);
        var editor = new PlaylistEditor(service);
        var playlist = editor.Submit(user.UserId, "Утренний микс", isPublic: true);
        playlist.AddTrack(new Track("t-01", "Northern Lights", "Ambient", new byte[] { 1, 2, 3 }));

        var player = new Player(new AudioOutput());
        var controller = new PlaybackController(player);
        Console.WriteLine(controller.PlayPlaylist(playlist.PlaylistId, user.UserId, user.Playlists, user.Subscriptions));
    }
}
