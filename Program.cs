using System.Collections;
using System.IO;
using System;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.VisualBasic;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using System.IO.Compression;
using System.Xml;


//READ PATH TO SQLITE FILE FROM USER
string SQLConnection = "Data Source=";
Console.Clear();
System.Console.WriteLine("This is a tool that can decrypt and convert all manga pages, in Firefox based browsers, which were previously downloaded by Siren extension");
System.Console.WriteLine("The extension or window dont have to be active, the process is totally offline; the mangas need to have been downloaded offline beforehand tho");
System.Console.WriteLine();
System.Console.WriteLine(@"Navigate to <User>\AppData\Roaming\Mozilla\Firefox\Profiles\<your profile>\storage\default\atsu.moe\idb");
System.Console.WriteLine("This is where Firefox stores downloaded binary data. You will find a few folders and some .sqlite files; in one of these folders are files named with just numbers and no extension.");
System.Console.WriteLine("Copy the *PATH* to the .sqlite file that has the same name as this folder and paste it below without double quotes:");
string sqlite_path = System.Console.ReadLine();
SQLConnection+= sqlite_path;
// CONNECT TO SQLITE and UNIQUE KEY?VALUE PAIR
Dictionary<string, string> UKVP = new Dictionary<string, string>(); //Unique manga_ids
Dictionary<string, byte[]> AKVP = new Dictionary<string, byte[]>(); //All pages in each chapter
try{
    using (var connection = new SqliteConnection(@SQLConnection)) //.sqlite file path
    {
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT key, file_ids, data FROM object_data"; //keys are manga_id_ch(n), file ids are all pages in each chapter ref, data is the blobs
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {                    
                string cryptedkey = reader.GetString(0);
                string key = DecryptKey(cryptedkey);

                //Optimise sequence size
                long size = reader.GetBytes(1, 0, null, 0, 0); //gets bytes length            
                byte[] all_pages = new byte[size];
                reader.GetBytes(1, 0, all_pages, 0, (int)size); //inserts the data into all_pages
                AKVP.Add(key, all_pages);
                if(!UKVP.ContainsKey(key.Substring(0, 6)))
                {
                    string builder = "";
                    size = reader.GetBytes(2, 0, null, 0, 0);
                    byte[] ukey = new byte[size];
                    reader.GetBytes(2, 0, ukey, 0, (int)size);
                    foreach(byte b in ukey)
                    {                    
                        //decrypt readable characters
                        string hex = b.ToString("X2");
                        string text = Encoding.ASCII.GetString(Convert.FromHexString(hex));
                        char check = (char)text[0];
                        if(char.IsLetterOrDigit(check) || char.IsPunctuation(check) || hex == "20" ) //20 is white space " "
                        {
                            builder = builder + check;
                        }                    
                    }
                    int index = builder.IndexOf("Title") + 7;
                    UKVP.Add(key.Substring(0, 6), builder.Substring(index, 35)); //this is the best i can do
                }
            }
        }
    }
} catch (Exception exc)
{
    Console.Clear();
    System.Console.WriteLine("Probably the file path isnt correct; Type 'Y' for more info");
    if (System.Console.ReadLine().Trim() == "Y")
    {
        System.Console.WriteLine(exc.Message);
        Console.ReadLine();
    }
}
//KEY DECRYPTER 
static string DecryptKey(string s)
{
    string decrypt = new string(s.Select(c => (char)(c - 1)).ToArray());
    return decrypt;
}

Console.Clear();
System.Console.WriteLine("The 'codes' you see are the links for the manga on atsu.moe website, they are 100% correct");
System.Console.WriteLine("The text below that should be something that closely represents the manga name, its really hard to decrypt and most likely wont match completely; Refer to the codes if the decryption fails");
System.Console.WriteLine();
System.Console.WriteLine("Type the number of the manga that you want downloaded");
System.Console.WriteLine();
int n = 0;
List<string> manga_ids = new List<string>();
foreach (KeyValuePair<string, string> s in UKVP)
{
    n++;
    manga_ids.Add(s.Key);
    int chapter_counter = 0;
    foreach(string all_chapters in AKVP.Keys)
    {
        if (all_chapters.Contains(s.Key))
        {
            chapter_counter++;
        }
    }
    System.Console.WriteLine(n + ". " + s.Key + " - " + chapter_counter + " downloaded chapters");
    System.Console.WriteLine(s.Value);
    System.Console.WriteLine();
}

//GET ALL THE FILES THAT NEED TO BE CONVERTED
int chosen = int.Parse(System.Console.ReadLine());
string all_webp_ids = "";
Console.Clear();
System.Console.WriteLine("Paste the folder destination you want your output to be in (without double quotes); the folder must already exist: ");
string folder_output = System.Console.ReadLine();
if(!Directory.Exists(folder_output))
{
    Directory.CreateDirectory(folder_output);
}
System.Console.WriteLine("Do you want to separate the chapters in different folders? Type 'Y' for yes");
if(System.Console.ReadLine().Trim() == "Y")
{
    //SEPARATE FOLDERS
    System.Console.WriteLine("Doing the magic...");
    foreach(KeyValuePair<string, byte[]> all_selected_chapters in AKVP)
    {
        try{
            if (all_selected_chapters.Key.Contains(manga_ids[chosen-1]))
            {
                all_webp_ids = Encoding.UTF8.GetString(all_selected_chapters.Value);
            }
        } catch(Exception exc)
        {
            Console.Clear();
            System.Console.WriteLine("The number wasnt in displayed range; Type 'Y' for more info");
            if (System.Console.ReadLine().Trim() == "Y")
            {
                System.Console.WriteLine(exc.Message);
            }
        }
    
        string folder_input = sqlite_path.Substring(0, sqlite_path.Length-6) + @"files\";
        foreach(string file_id in all_webp_ids.Split())
        {
            string tmp_input = folder_input + file_id;
            string output_checker = folder_output + @"\" + all_selected_chapters.Key;
            if(!Directory.Exists(output_checker))
            {
                Directory.CreateDirectory(output_checker);
            }
            string tmp_output = output_checker + @"\" + file_id + ".png";
            using(Image img = Image.Load(tmp_input))
            {
                img.Save(tmp_output, new PngEncoder());
            }
        }
    }
    System.Console.WriteLine("Do you also want .cbz files, a common manga extension; Type 'Y' in case you do");
    if(System.Console.ReadLine().Trim() == "Y")
    {
        System.Console.WriteLine("Paste the folder destination you want your output to be in, it cant be inside the folder from before (without double quotes): ");
        string zip_output = System.Console.ReadLine();
        if(!Directory.Exists(zip_output))
        {
            Directory.CreateDirectory(zip_output);
        }
        foreach(string directory in Directory.GetDirectories(folder_output))
        {
            ZipFile.CreateFromDirectory(directory, zip_output + @"\" + directory.Substring(directory.LastIndexOf(@"\") + 1) + ".cbz");
        }
    }
}
else //NO SEPARATE FOLDERS
{
    foreach(KeyValuePair<string, byte[]> all_selected_chapters in AKVP)
    {
        try{
            if (all_selected_chapters.Key.Contains(manga_ids[chosen-1]))
            {
                string chapter_pages = Encoding.UTF8.GetString(all_selected_chapters.Value);
                all_webp_ids += chapter_pages;
                all_webp_ids += " ";
            }
        } catch(Exception exc)
        {
            Console.Clear();
            System.Console.WriteLine("The number wasnt in displayed range; Type 'Y' for more info");
            if (System.Console.ReadLine().Trim() == "Y")
            {
                System.Console.WriteLine(exc.Message);
            }
        }
    }
    string[] all_webp_string = all_webp_ids.Split();
    string[] all_webp = all_webp_string.Take(all_webp_string.Length-1).ToArray(); //.Split creates an empty last slot

    //THE MAGIC FOR NO FOLDERS
    System.Console.WriteLine("Doing the magic...");
    string folder_input = sqlite_path.Substring(0, sqlite_path.Length-6) + @"files\";
    foreach(string file_id in all_webp)
    {
        string tmp_input = folder_input + file_id;
        string tmp_output = folder_output + @"\" + file_id + ".png";
        using(Image img = Image.Load(tmp_input))
        {
            img.Save(tmp_output, new PngEncoder());
        }
    }
    //.ZIP
    System.Console.WriteLine("Do you also want a .cbz file, a common manga extension; Type 'Y' in case you do");
    if(System.Console.ReadLine().Trim() == "Y")
    {
        System.Console.WriteLine("Paste the folder destination you want your output to be in, it cant be inside the folder from before (without double quotes): ");
        string zip_output = System.Console.ReadLine();
        if(!Directory.Exists(zip_output))
        {
            Directory.CreateDirectory(zip_output);
        }
        zip_output += @"\" + manga_ids[chosen-1].Substring(1) + ".cbz";
        string zip_input = folder_output + @"\";
        ZipFile.CreateFromDirectory(zip_input, zip_output);
    }
}
System.Console.WriteLine();
System.Console.WriteLine("Thank you for trusting my program. You can safely close the window.");