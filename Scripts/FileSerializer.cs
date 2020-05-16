using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public class FileSerializer
{
    public static bool SerializeToFile(string filePath, System.Object serializableObject)
    {
        BinaryFormatter bf = new BinaryFormatter();
        filePath = Path.Combine(Application.persistentDataPath, filePath);

        FileStream file = File.Open(filePath, FileMode.OpenOrCreate);
        if (file.CanWrite == false) return false;

        try
        {
            bf.Serialize(file, serializableObject);
        }
        catch {
            file.Close();
            return false;
        }
        file.Close();
        return true;
    }

    public static T DeserializeFromFile<T>(string filePath)
    {
        filePath = Path.Combine(Application.persistentDataPath, filePath);
        if (File.Exists(filePath) == false) return default(T);

        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Open(filePath, FileMode.Open);
        if (file.CanRead == false) return default(T);

        var result = default(T);
        try
        {
            result = (T)bf.Deserialize(file);
        }
        catch { }
        file.Close();
        return result;
    }
}
