using System.Collections.Generic;
using PM.Domain.World;
namespace PM.Persistence {
/// <summary>Порт файловой системы: позволяет тестам работать с памятью, Unity — с persistentDataPath.</summary>
public interface ISaveFilePort {
    bool Exists(string fileName);
    byte[] ReadAllBytes(string fileName);
    void WriteAllBytes(string fileName, byte[] data);
    void Delete(string fileName);
    void Move(string fromName, string toName);
}
/// <summary>Реализация на System.IO (Unity: persistentDataPath + относительное имя).</summary>
public sealed class SystemFilePort : ISaveFilePort {
    private readonly string _dir;
    public SystemFilePort(string directory) { _dir = directory; }
    private string Path(string name) => System.IO.Path.Combine(_dir, name);
    public bool Exists(string fileName) => System.IO.File.Exists(Path(fileName));
    public byte[] ReadAllBytes(string fileName) => System.IO.File.ReadAllBytes(Path(fileName));
    public void WriteAllBytes(string fileName, byte[] data) {
        if (!System.IO.Directory.Exists(_dir)) System.IO.Directory.CreateDirectory(_dir);
        System.IO.File.WriteAllBytes(Path(fileName), data);
    }
    public void Delete(string fileName) { if (Exists(fileName)) System.IO.File.Delete(Path(fileName)); }
    public void Move(string fromName, string toName) => System.IO.File.Move(Path(fromName), Path(toName));
}
}
