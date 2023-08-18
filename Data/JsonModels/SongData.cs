namespace UpLoader_For_ET.Models.JsonModels;

public class SongData
{
    public string FilePath  { get; set; }

    public string Artist { get; set; }

    public string SongName { get; set; }

    public SongData(string filePath, string artiste, string songName)
    {
        FilePath = filePath;
        Artist = artiste;
        SongName = songName;
    }
}


