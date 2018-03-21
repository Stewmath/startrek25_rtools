using System.IO;

public class Archive {
    string directory;

    public Archive(string d) {
        this.directory = d;
        if (!directory.EndsWith("/"))
            directory += '/';
    }

    public FileInfo GetFileInfo(string filename) {
        return new FileInfo(directory+filename);
    }

    public FileInfo[] GetAllFiles() {
        var dir = new DirectoryInfo(directory);
        return dir.GetFiles();
    }

    public System.IO.FileStream getFileReadStream(string filename) {
        filename = filename.ToUpper();
        return System.IO.File.OpenRead(directory + filename);
    }
};
