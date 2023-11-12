using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.FsSystem;

namespace GenerateSwitchDecTitleKey
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("No input file specified."); //TODO: add useful help text?
                return;
            }

            string keyFile = AppDomain.CurrentDomain.BaseDirectory + "prod.keys";

            if (!File.Exists(keyFile))
            {
                Console.WriteLine("prod.keys missing.");
                return;
            }

            foreach (string arg in args)
            {
                string zipFile = arg;

                Console.WriteLine("Processing file [" + zipFile + "]");

                if (string.IsNullOrEmpty(zipFile) || !File.Exists(zipFile))
                {
                    Console.WriteLine("Input does not exist.");
                    return;
                }

                KeySet keyset = ExternalKeyReader.ReadKeyFile(keyFile);

                using (ZipArchive zip = ZipFile.Open(zipFile, ZipArchiveMode.Read))
                {
                    //main nca
                    string mainNcaName = zip.Entries.OrderByDescending(x => x.Length).FirstOrDefault(x => x.Name.EndsWith(".nca")).Name;

                    //title key
                    string encTitleKeyName = zip.Entries.FirstOrDefault(x => x.Name.EndsWith(".enctitlekey.bin"))?.Name;
                    string titleKeyName = encTitleKeyName.Substring(0, encTitleKeyName.IndexOf('.')).ToLower();

                    //only checking if we need to extract the dectitlekey or not
                    if (zip.Entries.Any(x => x.Name.EndsWith(".dectitlekey.bin")))
                    {
                        Console.WriteLine("No need to extract key, already exists in zip.");
                        return;
                    }

                    //get the encrypted title key
                    ZipArchiveEntry encEntry = zip.Entries.FirstOrDefault(x => x.Name == encTitleKeyName);

                    if (encEntry != null)
                    {
                        using (Stream unzippedEntryStream = encEntry.Open())
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                unzippedEntryStream.CopyTo(ms);
                                string encTitleKey = Convert.ToHexString(ms.ToArray());

                                string encKey = titleKeyName + " = " + encTitleKey;

                                //add the key to the list
                                using (MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(encKey ?? "")))
                                {
                                    ExternalKeyReader.ReadTitleKeys(keyset, memoryStream);
                                }
                            }
                        }
                    }

                    //get the main nca
                    ZipArchiveEntry mainNcaEntry = zip.Entries.FirstOrDefault(x => x.Name == mainNcaName);

                    if (mainNcaEntry != null)
                    {
                        //using (MemoryStream ms = new MemoryStream())
                        //{
                            //reads the whole file into memory, takes about 25s, will run out of memory
                            using (Stream unzippedEntryStream = mainNcaEntry.Open())
                            {
                                //we just need the first amount of bytes
                                byte[] chunk = new byte[10000];
                                unzippedEntryStream.Read(chunk, 0, 10000);
                                MemoryStream byteStream = new MemoryStream(chunk);

                                //copying the whole stream runs out of memory
                                //Console.WriteLine("Reading nca into memory, this might take a while and use a lot of RAM");
                                //unzippedEntryStream.CopyTo(ms);

                                using (IStorage ncaInFile = new StreamStorage(byteStream, true))
                                {
                                    Nca nca = new Nca(keyset, ncaInFile);

                                    try
                                    {
                                        byte[] decTitleKey = nca.GetDecryptedTitleKey();
                                        string decryptedTitleKey = Convert.ToHexString(decTitleKey);

                                        string decryptedFilePath = AppDomain.CurrentDomain.BaseDirectory + titleKeyName + ".dectitlekey.bin";

                                        File.WriteAllBytes(decryptedFilePath, decTitleKey);
                                    }
                                    catch (MissingKeyException)
                                    {
                                        //silent
                                    }
                                }
                            }
                        //}
                    }

                    Console.WriteLine("Done with [" + zipFile + "]");
                }
            }

            return;
        }
    }
}
