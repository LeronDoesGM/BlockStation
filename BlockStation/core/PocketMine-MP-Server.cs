﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Threading;
using System.IO;
using Microsoft.VisualBasic;
using System.Windows;
using System.ComponentModel;
using fNbt;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BlockStation
{
    public class PlayerList
    {
        private List<Player> List;
        string path;
        IDictionary<string, Player> PlayerIndex;

        // Konstruktor
        public PlayerList(string dir, ref IDictionary<string, Player> i)
        {
            PlayerIndex = i;
            path = dir;
            List = new List<Player>();
            ReadList();
        }

        public void ReadList()
        {
            List.Clear();
            StreamReader file = new StreamReader(path);
            string line;

            try
            {
                while ((line = file.ReadLine()) != null)
                {
                    if (line != "")
                    {
                        Player tmp;
                        PlayerIndex.TryGetValue(line, out tmp);

                        // Wenn spieler auf der liste, aber nicht im index
                        if (tmp == null)
                        {
                            tmp = new Player(line);
                            PlayerIndex.Add(tmp.Name, tmp);
                        }
                        List.Add(tmp);
                    }
                }
                file.Close();
            }
            catch (NullReferenceException)
            {
                Utils.ShowReadingError(path);
                Environment.Exit(1);
            }
            catch (FileNotFoundException)
            {
                Utils.ShowFileMissingError(path);
                Environment.Exit(1);
            }
            catch (FileFormatException)
            {
                Utils.ShowFormatError(path);
                Environment.Exit(1);
            }
            catch (FileLoadException)
            {
                Utils.ShowLoadError(path);
                Environment.Exit(1);
            }
        }

        public void addToList(Player player)
        {
            if (!(PlayerIndex.ContainsKey(player.Name)))
            {
                PlayerIndex.Add(player.Name, player);
            }

            if(!List.Contains(player))
                List.Add(player);

            try
            {
                File.WriteAllLines(path, List.ConvertAll(Convert.ToString));
            }
            catch (NullReferenceException)
            {
                Utils.ShowReadingError(path);
                Environment.Exit(1);
            }
            catch (FileNotFoundException)
            {
                Utils.ShowFileMissingError(path);
                Environment.Exit(1);
            }
            catch (FileFormatException)
            {
                Utils.ShowFormatError(path);
                Environment.Exit(1);
            }
            catch (FileLoadException)
            {
                Utils.ShowLoadError(path);
                Environment.Exit(1);
            }
        }

        public void removeFromList(Player player)
        {
            List.Remove(player);

            try
            {
                File.WriteAllLines(path, List.ConvertAll(Convert.ToString));
            }
            catch (NullReferenceException)
            {
                Utils.ShowReadingError(path);
                Environment.Exit(1);
            }
            catch (FileNotFoundException)
            {
                Utils.ShowFileMissingError(path);
                Environment.Exit(1);
            }
            catch (FileFormatException)
            {
                Utils.ShowFormatError(path);
                Environment.Exit(1);
            }
            catch (FileLoadException)
            {
                Utils.ShowLoadError(path);
                Environment.Exit(1);
            }
        }

        public List<Player> getListData()
        {
            return List;
        }
    }

    public class Player
    {
        public Player(string n)
        {
            Name = n;
        }

        public string Name { get; set; }
        public short Health { get; set; }
        public DateTime LastOnline { get; set; }
        public DateTime FirstTimeOnline { get; set; }
        public bool Online { get; set; }

        public override string ToString()
        {
            return this.Name;
        }

    }

    public class PocketMine_MP_Server
    {
        // Konstruktor
        public PocketMine_MP_Server(string d)
        {
            // Initialisierungen
            PlayerIndex = new Dictionary<string, Player>();
            query = new Query.MCQuery();
            dir = d;

            Whitelist = new PlayerList(dir + fn_whitelist, ref PlayerIndex);
            OPList = new PlayerList(dir + fn_oplist, ref PlayerIndex);
            IPBanList = new PlayerList(dir + fn_bannedip, ref PlayerIndex);
            BanList = new PlayerList(dir + fn_bannedplayer, ref PlayerIndex);

            // OnlineCheck Hintergrund Thread.
            worker = new BackgroundWorker();
            worker.DoWork += new DoWorkEventHandler(CheckServerAvailability);

            ReadServerSettings();
            ReadPlayerNBTData();
        }

        // Onlinepic


        // Konstanten (fn = FileName)
        const string fn_whitelist = "white-list.txt";
        const string fn_serverproperties = "server.properties";
        const string fn_oplist = "ops.txt";
        const string fn_pocketmine = "pocketmine.yml";
        const string fn_playerdat = "players/*.dat";
        const string fn_bannedplayer = "banned-players.txt";
        const string fn_bannedip = "banned-ips.txt";

        // Events
        public event EventHandler ServerStarted;
        public event EventHandler ProcessStopped;
        public event EventHandler ServerCrash;
        public event EventHandler ServerOutputChanged;
        public event EventHandler PlayerJoinedServer;
        public event EventHandler PlayerLeaveServer;
        public event EventHandler ServerGetOnline;
        public event EventHandler ServerGetOffline;
        public event EventHandler NewChatMessage;

        // Alle Spieler
        public IDictionary<string, Player> PlayerIndex;

        // Spieler Listen
        public PlayerList Whitelist;
        public PlayerList OPList;
        public PlayerList BanList;
        public PlayerList IPBanList;

        // Prozess für PocketMine
        private Process PocketMineProcess;

        // Backgroundworker für lastige Aufgaben
        private BackgroundWorker worker;

        // Letzte geschickte Nachricht (0 = /, 1 = Sender, 2 = Text)
        public string[] message = { "0", "1", "2", "3"};

        // Query
        Query.MCQuery query;

        // Public Server Properties
        public bool ServerProcess
        {
            get
            {
                return serverprocess;
            }
            set
            {
                serverprocess = value;
            }
        }
        public string ServerOutput
        {
            get
            {
                return serveroutput;
            }
        }
        public int OnlinePlayers
        {
            get
            {
                var info = query.Info();
                if (Online)
                {
                    return info.OnlinePlayers;
                }
                else
                {
                    return 0;
                }
            }
        }
        public int Latency
        {
            get
            {   
                var info = query.Info();
                if (Online)
                {
                    return info.Latency;

                }
                else
                {
                    return 0;

                }
            }
        }
        public string Version
        {
            get
            {
                var info = query.Info();
                if (Online)
                {
                    return info.Version;
                }
                else
                {
                    return "";
                }
            }
        }
        public bool Online
        {
            get {
                if (worker.IsBusy)
                {
                    return online;
                }
                else
                {
                    worker.RunWorkerAsync();
                    return online;
                }
            }
            set {
                if (online == true && value == false)
                {
                    if (ServerGetOffline != null)
                    {
                        ServerGetOffline.Invoke(this, EventArgs.Empty);
                    }
                }
                if (online == false && value == true)
                {
                    if(ServerGetOnline != null)
                    {
                        ServerGetOnline.Invoke(this, EventArgs.Empty);
                        ServerStarted.Invoke(this, null);
                    }
                }
                online = value;
            }
        }
        public string Name
        {
            get { return prop_server_name; }
            set { prop_server_name = value; WriteServerSettings(); }
        }
        public int Port
        {
            get { return Int32.Parse(prop_server_port); }
            set { prop_server_port = value.ToString(); WriteServerSettings(); }
        }
        public int MaxPlayers
        {
            get { return Int32.Parse(prop_max_players); }
            set { prop_max_players = value.ToString(); }
        }
        public string Motd
        {
            get { return prop_motd; }
            set { prop_motd = value; WriteServerSettings(); }
        }
        public bool EnableQuery
        {
            get {
                if (prop_enable_query == "on")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            set {
                if (value == false)
                {
                    prop_enable_query = "off";
                }
                else
                {
                    prop_enable_query = "on";
                }
                WriteServerSettings();
            }
        }
        public bool EnableRCON
        {
            get
            {
                if (prop_enable_rcon == "on")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                if (value == false)
                {
                    prop_enable_rcon = "off";
                }
                else
                {
                    prop_enable_rcon = "on";
                }
                WriteServerSettings();
            }
        }
        public bool EnableWhitelist
        {
            get {
                if(prop_white_list == "1")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            set {
                if(value == true)
                {
                    prop_white_list = "1";
                }
                else
                {
                    prop_white_list = "0";
                }
                WriteServerSettings();
            }
        }
        public int SpawnProtectionRadius
        {
            get { return Int32.Parse(prop_spawn_protection); }
            set { prop_spawn_protection = value.ToString(); WriteServerSettings(); }
        }
        public string WorldName
        {
            get { return prop_level_name; }
            set { prop_level_name = value.ToString(); WriteServerSettings(); }
        }
        public string GeneratorSettings
        {
            get { return prop_generator_settings; }
            set { prop_generator_settings = value.ToString(); WriteServerSettings(); }
        }
        public bool EnablePlayerAchievements
        {
            get {
                if(prop_announce_player_achievements == "on")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            set {
                if(value == true)
                {
                    prop_announce_player_achievements = "on";
                }
                else
                {
                    prop_announce_player_achievements = "off";
                }
                WriteServerSettings();
            }
        }
        public bool SpawnAnimals
        {
            get
            {
                if (prop_spawn_animals == "on")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                if (value == true)
                {
                    prop_spawn_animals = "on";
                }
                else
                {
                    prop_spawn_animals = "off";
                }
                WriteServerSettings();
            }
        }
        public int Difficulty
        {
            get { return Int32.Parse(prop_difficulty); }
            set { prop_difficulty = value.ToString(); WriteServerSettings(); }
        }
        public string WorldSeed
        {
            get { return prop_level_seed; }
            set { prop_level_seed = value.ToString(); WriteServerSettings(); }
        }
        public bool EnablePVP
        {
            get
            {
                if (prop_pvp == "on")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                if (value == true)
                {
                    prop_pvp = "on";
                }
                else
                {
                    prop_pvp = "off";
                }
                WriteServerSettings();
            }
        }
        public string MemoryLimit
        {
            get { return prop_memory_limit; }
            set { prop_memory_limit = value; WriteServerSettings(); }
        }
        public string RCONPassword
        {
            get { return prop_rcon_password; }
            set { prop_rcon_password = value.ToString(); WriteServerSettings(); }
        }
        public bool EnableAutoSave
        {
            get
            {
                if (prop_auto_save == "on")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                if (value == true)
                {
                    prop_auto_save = "on";
                }
                else
                {
                    prop_auto_save = "off";
                }
                WriteServerSettings();
            }
        }
        public bool EnableHardcore
        {
            get
            {
                if (prop_hardcore == "on")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                if (value == true)
                {
                    prop_hardcore = "on";
                }
                else
                {
                    prop_hardcore = "off";
                }
                WriteServerSettings();
            }
        }
        public bool ForceGamemode
        {
            get
            {
                if (prop_force_gamemode == "on")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                if (value == true)
                {
                    prop_force_gamemode = "on";
                }
                else
                {
                    prop_force_gamemode = "off";
                }
                WriteServerSettings();
            }
        }
        public bool SpawnMobs
        {
            get
            {
                if (prop_spawn_mobs == "on")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                if (value == true)
                {
                    prop_spawn_mobs = "on";
                }
                else
                {
                    prop_spawn_mobs = "off";
                }
                WriteServerSettings();
            }
        }
        public bool AllowFlight
        {
            get
            {
                if (prop_allow_flight == "on")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                if (value == true)
                {
                    prop_allow_flight = "on";
                }
                else
                {
                    prop_allow_flight = "off";
                }
                WriteServerSettings();
            }
        }
        public int Gamemode
        {
            get { return Int32.Parse(prop_gamemode); }
            set { prop_gamemode = value.ToString(); WriteServerSettings(); }
        }
        public string WorldType
        {
            get { return prop_level_type; }
            set { prop_level_type = value.ToString(); WriteServerSettings(); }
        }
        public string Directory
        {
            get { return dir; }
            set { dir = value; }
        }

        //Server einstellungen
        private bool serverprocess;
        private string serveroutput;
        private bool online = false;
        private string dir;
        private string prop_server_name = "";
        private string prop_server_port = "";
        private string prop_max_players = "";
        private string prop_motd = "";
        private string prop_spawn_mobs = "";
        private string prop_allow_flight = "";
        private string prop_enable_query = "";
        private string prop_enable_rcon = "";
        private string prop_white_list = "";
        private string prop_spawn_protection = "";
        private string prop_level_name = "";
        private string prop_generator_settings = "";
        private string prop_announce_player_achievements = "";
        private string prop_spawn_animals = "";
        private string prop_difficulty = "";
        private string prop_level_seed = "";
        private string prop_pvp = "";
        private string prop_memory_limit = "";
        private string prop_rcon_password = "";
        private string prop_auto_save = "";
        private string prop_hardcore = "";
        private string prop_force_gamemode = "";
        private string prop_gamemode = "";
        private string prop_level_type = "";

        public Player getPlayer(string name)
        {
            Player player;
            PlayerIndex.TryGetValue(name, out player);
            return player;
        }

        // Startet den Server
        public void Start ()
        {
            if (PocketMineProcess == null)
            {
                PocketMineProcess = new Process();
                PocketMineProcess.StartInfo.CreateNoWindow = true;
                PocketMineProcess.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                PocketMineProcess.StartInfo.WorkingDirectory = dir;
                PocketMineProcess.StartInfo.FileName = "cmd.exe";
                PocketMineProcess.StartInfo.UseShellExecute = false;
                PocketMineProcess.StartInfo.RedirectStandardInput = true;
                PocketMineProcess.StartInfo.RedirectStandardOutput = true;
                PocketMineProcess.OutputDataReceived += OutputDataReceived;

                try
                {
                    PocketMineProcess.Start();
                }
                catch(Exception)
                {
                    Utils.ShowStartProcessError("PocketMine");
                }

                PocketMineProcess.StandardInput.WriteLine("bin\\php\\php.exe " + "PocketMine-MP.phar --disable-ansi %*");
                PocketMineProcess.BeginOutputReadLine();
            }
        }

        // Lädt den Server neu
        public void Reload()
        {
            if (PocketMineProcess != null)
                PocketMineProcess.StandardInput.WriteLine("reload");
        }

        // Fährt den Server herunter bzw. stopt ihn
        public void Stop()
        {
            BackgroundWorker shutdown = new BackgroundWorker();
            shutdown.DoWork += backgroundworker_stop;
            shutdown.RunWorkerAsync();
            WriteServerSettings();
        }

        // Backgroundworker für Stop, da das stoppen länger dauert.
        private void backgroundworker_stop(object sender, DoWorkEventArgs e)
        {
            if (PocketMineProcess != null)
            {
                WriteServerSettings();
                PocketMineProcess.StandardInput.WriteLine("stop");
                WriteServerSettings();
                System.Threading.Thread.Sleep(3000);
                PocketMineProcess.Close();
                PocketMineProcess = null;
                CheckServerAvailability(this, null);
                if (PocketMineProcess == null)
                {
                    ProcessStopped.Invoke(this, null);
                }
                WriteServerSettings();
            }
            
        }

        // Prüft die Server Verfügbarkeit
        private void CheckServerAvailability(object sender, DoWorkEventArgs e)
        {
            // Wenn der Prozess nicht läuft, läuft auch kein server!
            if (PocketMineProcess == null)
            {
                ServerProcess = false;
                Online = false;
            }
            else
            {
                var info = query.Info();
                query.Connect("localhost", Port);
                // Wenn die Query verbindung hergestellt werden kann, läuft der Server!
                if (query.Success())
                {
                    Online = true;
                    ServerProcess = true;
                }
                else
                {
                    Online = false;
                    ServerProcess = false;
                }
                CheckPlayerStatus();
            }
        }

        // Speichert die Serverausgabe im ServerOutput String
        private void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if(e.Data != null)
            {
                // Spieler ist beigetreten
                if (e.Data.Contains("joined"))
                {
                    PlayerJoinedServer.Invoke(this, EventArgs.Empty);
                    ReadPlayerNBTData();
                }
                // Spieler hat den Server verlassen
                if (e.Data.Contains("logged out"))
                {
                    PlayerLeaveServer.Invoke(this, EventArgs.Empty);
                    ReadPlayerNBTData();
                }
                // Nachricht vom Admin
                if (e.Data.Contains(" [Server] "))
                {
                    string[] stringSeparators = new string[] { " [Server] " };
                    string[] temp = e.Data.ToString().Split(stringSeparators, StringSplitOptions.None);
                    string text = temp[1];

                    message[1] = "ADMIN";
                    message[2] = " " + text;

                    NewChatMessage.Invoke(this, EventArgs.Empty);
                }
                // Nachricht von einem normalen Spieler
                if (e.Data.Contains("<") && e.Data.Contains(">"))
                {
                    message = e.Data.ToString().Split('<','>');
                    NewChatMessage.Invoke(this, EventArgs.Empty);
                }
            }

            

        serveroutput += "\n";
            serveroutput += e.Data;

            if (serveroutput.Length > 20000)
                serveroutput = serveroutput.Substring(20000, 0);
            ServerOutputChanged.Invoke(this, EventArgs.Empty);
        }

        // Sendet ein Befehl an den Server
        public void SendCommand (string command)
        {
            if(Online)
                PocketMineProcess.StandardInput.WriteLine(command);
        }

        // Lese Servereinstellungen
        public void ReadServerSettings()
        {
            try
            {
                var data = new Dictionary<string, string>();
                foreach (var row in File.ReadAllLines(dir + fn_serverproperties))
                {
                    data.Add(row.Split('=')[0], string.Join("=", row.Split('=').Skip(1).ToArray()));
                }


                prop_server_name = data["server-name"];
                prop_server_port = data["server-port"];
                prop_allow_flight = data["allow-flight"];
                prop_announce_player_achievements = data["announce-player-achievements"];
                prop_pvp = data["pvp"];
                prop_memory_limit = data["memory-limit"];
                prop_force_gamemode = data["force-gamemode"];
                prop_spawn_protection = data["spawn-protection"];
                prop_level_name = data["level-name"];
                prop_generator_settings = data["generator-settings"];
                prop_rcon_password = data["rcon.password"];
                prop_enable_rcon = data["enable-rcon"];
                prop_spawn_animals = data["spawn-animals"];
                prop_difficulty = data["difficulty"];
                prop_level_seed = data["level-seed"];
                prop_spawn_mobs = data["spawn-mobs"];
                prop_auto_save = data["auto-save"];
                prop_hardcore = data["hardcore"];
                prop_white_list = data["white-list"];
                prop_enable_query = data["enable-query"];
                prop_level_type = data["level-type"];
                prop_motd = data["motd"];
                prop_max_players = data["max-players"];
                prop_gamemode = data["gamemode"];
            }
            catch (NullReferenceException)
            {
                Utils.ShowReadingError(fn_serverproperties);
                Environment.Exit(1);
            }
            catch (FileNotFoundException)
            {
                Utils.ShowFileMissingError(fn_serverproperties);
                Environment.Exit(1);
            }
            catch (FileFormatException)
            {
                Utils.ShowFormatError(fn_serverproperties);
                Environment.Exit(1);
            }
            catch (FileLoadException)
            {
                Utils.ShowLoadError(fn_serverproperties);
                Environment.Exit(1);
            }
        }

        // Schreibe Servereinstellungen
        public void WriteServerSettings()
        {
            try
            {
                string[] lines = {
                    "server-name=" + prop_server_name,
                    "server-port=" + prop_server_port,
                    "memory-limit=" + prop_memory_limit,
                    "gamemode=" + prop_gamemode,
                    "max-players=" + prop_max_players,
                    "spawn-protection=" + prop_spawn_protection,
                    "white-list=" + prop_white_list,
                    "enable-query=" + prop_enable_query,
                    "enable-rcon=" + prop_enable_rcon,
                    "motd=" + prop_motd,
                    "announce-player-achievements=" + prop_announce_player_achievements,
                    "allow-flight=" + prop_allow_flight,
                    "spawn-animals=" + prop_spawn_animals,
                    "spawn-mobs=" + prop_spawn_mobs,
                    "force-gamemode=" + prop_force_gamemode,
                    "hardcore=" + prop_hardcore,
                    "pvp=" + prop_pvp,
                    "difficulty=" + prop_difficulty,
                    "generator-settings" + prop_generator_settings,
                    "level-name=" + prop_level_name,
                    "level-seed=" + prop_level_seed,
                    "level-type=" + prop_level_type,
                    "rcon.password=" + prop_rcon_password,
                    "auto-save=" + prop_auto_save
                };

                System.IO.File.WriteAllLines(dir + fn_serverproperties, lines, System.Text.Encoding.Default);
                //ReadServerSettings();
            }
            catch (NullReferenceException)
            {
                Utils.ShowReadingError(fn_serverproperties);
                Environment.Exit(1);
            }
            catch (FileNotFoundException)
            {
                Utils.ShowFileMissingError(fn_serverproperties);
                Environment.Exit(1);
            }
            catch (FileFormatException)
            {
                Utils.ShowFormatError(fn_serverproperties);
                Environment.Exit(1);
            }
            catch (FileLoadException)
            {
                Utils.ShowLoadError(fn_serverproperties);
                Environment.Exit(1);
            }
            catch
            {
                
            }
        }

        // Spieler zur Whitelist hinzufügen
        public void AddPlayerToWhitelist(string playername)
        {
            Player player;
            PlayerIndex.TryGetValue(playername, out player);

            if(player == null)
            {
                player = new Player(playername);
            }
            Whitelist.addToList(player);
            if (Online)
            {
                SendCommand("whitelist add " + playername);
            }
        }

        // Spieler von der Whitelist entfernen
        public void RemovePlayerFromWhitelist (string playername)
        {
            Player player;
            PlayerIndex.TryGetValue(playername, out player);
            Whitelist.removeFromList(player);

            if (Online)
            {
                SendCommand("whitelist remove " + playername);
            }
        }

        // Spieler zur Whitelist hinzufügen
        public void AddPlayerToOPList(string playername)
        {
            Player player;
            PlayerIndex.TryGetValue(playername, out player);

            if (player == null)
            {
                player = new Player(playername);
            }

            OPList.addToList(player);
            if (Online)
            {
                SendCommand("op " + playername);
            }
        }

        // Spieler von der Whitelist entfernen
        public void RemovePlayerFromOPList(string playername)
        {
            Player player;
            PlayerIndex.TryGetValue(playername, out player);

            OPList.removeFromList(player);
            if (Online)
            {
                SendCommand("deop " + playername);
            }
        }

        // Liest alle Spieler ein
        public void ReadPlayerNBTData()
        {
            string[] PlayerDataPath = System.IO.Directory.GetFiles(dir + "\\players\\", "*.dat");

            // Listen wieder zurücksetzen
            PlayerIndex.Clear();

            int counter = 0;

            // Lesen der Spielerdaten
            try
            {
                foreach (string path in PlayerDataPath)
                {
                    var myFile = new NbtFile();
                    myFile.LoadFromFile(path);
                    var Tag = myFile.RootTag;

                    Player tmp = new Player(Tag.Get<NbtString>("NameTag").Value);

                    long a1 = Tag.Get<NbtLong>("lastPlayed").Value;
                    double a2 = Convert.ToDouble(a1);
                    tmp.LastOnline = Utils.JavaTimeStampToDateTime(a2);

                    long b1 = Tag.Get<NbtLong>("firstPlayed").Value;
                    double b2 = Convert.ToDouble(b1);
                    tmp.FirstTimeOnline = Utils.JavaTimeStampToDateTime(b2);


                    PlayerIndex.Add(Tag.Get<NbtString>("NameTag").Value, tmp);
                    counter++;
                }
            }
            catch (NullReferenceException)
            {
                Utils.ShowReadingError(fn_playerdat);
                Environment.Exit(1);
            }
            catch (FileNotFoundException)
            {
                Utils.ShowFileMissingError(fn_playerdat);
                Environment.Exit(1);
            }
            catch (FileFormatException)
            {
                Utils.ShowFormatError(fn_playerdat);
                Environment.Exit(1);
            }
            catch (FileLoadException)
            {
                Utils.ShowLoadError(fn_playerdat);
                Environment.Exit(1);
            }
            Whitelist.ReadList();
            OPList.ReadList();
        }

        // Prüft ob die Spieler online sind
        public void CheckPlayerStatus()
        {
            var info = query.Info();
            if (Online)
            {
                foreach(Player p in PlayerIndex.Values)
                {
                    p.Online = false;
                }

                foreach(string name in info.Players)
                {
                    Player p = getPlayer(name);
                    p.Online = true;
                }

            }
        }

        // Wirft einen Spieler aus dem server raus
        public void KickPlayer(Player p, string reason = "")
        {
            SendCommand("kick " + p.Name + reason);
        }

        // Bannt einen Spieler
        public void BanPlayer(Player p, string reason = "")
        {
            if (Online)
            {
                SendCommand("ban " + p.Name + " " + reason);
            }
        }

        // DeBannt einen Spieler
        public void PardonPlayer(Player p)
        {
            if (Online)
            {
                SendCommand("pardon " + p.Name);
            }
        }

        // Tötet einen Spieler
        public void KillPlayer(Player p)
        {
            SendCommand("kill " + p.Name);
        }
    }

}
