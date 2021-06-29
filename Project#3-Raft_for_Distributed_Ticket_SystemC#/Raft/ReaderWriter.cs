using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Raft.ReaderWriter
{
    public class ReadWriteState
    {
        private static object sLock = new object();
        private static object sLockWrite = new object();
        private static object sLockRead = new object();
        public static void Write(string dir, string name, object obj){
            lock (sLock)
            {
                string path = Path.Combine(new string[] {@".", $"{dir}", $"{name}.b"});
                Directory.CreateDirectory(Path.Combine(new string[] {@".", $"{dir}"}));
                IFormatter formatter = new BinaryFormatter();
                Stream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                formatter.Serialize(stream, obj);
                stream.Close();
            }
        }

        public static object Read(string dir, string name)
        {
            lock(sLock)
            {
                string path = Path.Combine(new string[]{@".", $"{dir}", $"{name}.b"});
                if (File.Exists(path))
                {
                    IFormatter formatter = new BinaryFormatter();  
                    Directory.CreateDirectory(Path.Combine(new string[]{@".", $"{dir}"}));
                    Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);  
                    object obj = formatter.Deserialize(stream);  
                    stream.Close();

                    return obj;
                }
            }
            return null;
        }

        public static bool IfExists(string dir, string name)
        {
            var path = Path.Combine(new string[]{@".", $"{dir}", $"{name}.b"});
            return File.Exists(path);
        }

        public static void Test(string[] args)
        {
            List<string> lst = new List<string>();
            lst.Add("best of luck");
            lst.Add("best of luck");
            lst.Add("best of luck");
            lst.Add("best of luck"); 

            ReadWriteState.Write("node1", "log", lst);

            List<string> Snd = (List<string>) ReadWriteState.Read("node1", "log");

            Snd.ForEach(l => Console.WriteLine(l));
        }
    }
}