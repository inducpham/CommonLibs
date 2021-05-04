using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BinaryJSONSerializer : MonoBehaviour
{

    static string SEED = "V@#IXS0!@/,.ASD123";

    //public class Test
    //{
    //    public Dictionary<string, int> dict = new Dictionary<string, int>();
    //}

    //private void Start()
    //{
    //    var test = new Test();
    //    test.dict["a"] = 22;

    //    WriteToFile("", "test.bin", test);
    //    test = ReadFromFile<Test>("", "test.bin");
    //    Debug.Log(test.dict["a"]);
    //}

    public static void WriteToFile(string seed, string path, System.Object obj)
    {
        path = Path.Combine(Application.persistentDataPath, path);
        var content = Newtonsoft.Json.JsonConvert.SerializeObject(obj);
        var seedbytes = StringToBytes(seed + SEED);
        var seedlen = seedbytes.Length;
        var contentbytes = StringToBytes(content);
        for (var i = 0; i < contentbytes.Length; i++)
            contentbytes[i] = (byte) (contentbytes[i] + seedbytes[i % seedlen]);
        System.IO.File.WriteAllBytes(path, contentbytes);
    }

    public static T ReadFromFile<T>(string seed, string path)
    {
        path = Path.Combine(Application.persistentDataPath, path);
        byte[] contentbytes;
        try
        {
            contentbytes = System.IO.File.ReadAllBytes(path);
        }
        catch (Exception)
        {
            return default(T);
        }

        var seedbytes = StringToBytes(seed + SEED);
        var seedlen = seedbytes.Length;
        for (var i = 0; i < contentbytes.Length; i++)
            contentbytes[i] = (byte)(contentbytes[i] - seedbytes[i % seedlen]);

        string content = BytesToString(contentbytes);
        try
        {
            T result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(content);
            return result;
        } catch
        {
            return default(T);
        }
    }

    static byte[] StringToBytes(string str)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        return bytes;
    }

    static string BytesToString(byte[] bytes)
    {
        var result = System.Text.Encoding.UTF8.GetString(bytes);
        return result;
    }
}
