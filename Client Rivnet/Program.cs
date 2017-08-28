using System;
using System.Drawing;
using System.Net;
using System.IO;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.Management;
using ConfigurationParser;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace ClientRivnet
{
    
    public class ClientRivnetApp : Form
    {
        [STAThread]
        public static void Main()
        {
            Application.Run(new ClientRivnetApp());
        }
 
        private NotifyIcon  trayIcon;
        private ContextMenuStrip trayMenu;
        
        private string path;
        private bool activated;
        private string interfaceName;
        private List<string> rivnetIPs;
        private bool rivnetRequesting;
        private const string section = "Section Heading";
        
        public ClientRivnetApp()
        {
        	
        	ToolStripDropDown test = new ToolStripDropDown();
        	
            // Create a simple tray menu with only one item.
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Etat: ", null, null);
            trayMenu.Items.Add("Interface", null, null);
            trayMenu.Items.Add("Activer", null, onActivate);
            trayMenu.Items.Add("Desactiver", null, onDesactivate);
            trayMenu.Items.Add("Refresh", null, onRefresh);
            trayMenu.Items.Add("Exit", null, OnExit);
 
            // Create a tray icon. In this example we use a
            // standard system icon for simplicity, but you
            // can of course use your own custom icon too.
            trayIcon      = new NotifyIcon();
            trayIcon.Text = "Rivnet client";
            trayIcon.Icon = new Icon("rivnet.ico");
 
            // Add menu to tray icon and show it.
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible     = true;
            
            path="rivnet.conf";
            activated=false;
            interfaceName="";
            rivnetIPs=new List<string>();
            rivnetRequesting=true;

            read();

            activate();
            
        }

        private void changeInterface(string intName)
        {
            
        	interfaceName = intName;         
        	write();
            refresh();
        	
        }

        private void activate()
        {
            activated = true;         
        	write();
            refresh();
        }
        
        
        private void desactivate()
        {
            activated = false;            
        	write();
            refresh();
        }
        
        
        private async System.Threading.Tasks.Task refresh()
        {

            read();
        	
        	trayMenu.SuspendLayout();
        	
        	//Refreshing 'Etat'
        	trayMenu.Items[0].Text = "Etat: " + ( activated ? "Activé" : "Désactivé");
        	
        	//Displaying the good buttons to activate/desactivate.
            trayMenu.Items[2].Enabled = !activated;
            trayMenu.Items[2].Visible = !activated;
            
            trayMenu.Items[3].Enabled = activated;
            trayMenu.Items[3].Visible = activated;
            
            trayMenu.ResumeLayout();

            //Refreshing interface list
            string localIP = "";
    		string netmask = "";
    		string mac = "";
        	string gateway = "";
            (trayMenu.Items[1] as ToolStripMenuItem).DropDownItems.Clear();
    		foreach(NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
			{
				if(ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
				{
        			(trayMenu.Items[1] as ToolStripMenuItem).DropDownItems.Add(ni.Name, null, onChangeInterface);
        			if(ni.Name == interfaceName)
        			{
        				mac = tokenize(ni.GetPhysicalAddress().ToString());
        				foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
						{
						   if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
						   {
						       	localIP += "; " + ip.Address.ToString();
						   		netmask += "; " + ip.IPv4Mask;
						   }				   
						}
        			}
				}
            }

            //Is interface checked?
            foreach (ToolStripItem t in (trayMenu.Items[1] as ToolStripMenuItem).DropDownItems)
        	{
        		        		
        		(t as ToolStripMenuItem).Checked = (t.Text == interfaceName);
        		
        	}

            //Getting Rivnet informations
            if (rivnetIPs.Count != 0 && rivnetRequesting)
            {

                bool contacted = false;
                foreach (string ip in rivnetIPs)
                {

                    try
                    {

                        //=================== Rivnet servers ==================

                        string url_servers = "http://" + ip + "/clients_config/servers/" + mac;
                        
                        log(url_servers);
                        HttpWebRequest request_servers = (HttpWebRequest)WebRequest.Create(url_servers);
                        //HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        var response_servers = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request_servers.BeginGetResponse, request_servers.EndGetResponse, null).ConfigureAwait(false);
                        Stream resStream_servers = response_servers.GetResponseStream();

                        // If it reaches that point, the server responded

                        StreamReader reader_servers = new StreamReader(resStream_servers);

                        string servers = reader_servers.ReadToEnd();

                        log("Rivnet servers: " + servers);
                        rivnetIPs = servers.Split(',').ToList();
                        contacted = true;

                        write();

                        //=================== Gateway ==================

                        string url_gateway = "http://" + ip + "/clients_config/gateway/" + mac;

                        trayMenu.Items[0].ToolTipText = "Interface: " + interfaceName + "\nMac: " + mac + "\nIp: " + localIP + "\nNetmask: " + netmask + "\nGw: " + gateway;

                        log(url_gateway);
                        HttpWebRequest request_gateway = (HttpWebRequest)WebRequest.Create(url_gateway);
                        //HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        var response_gateway = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request_gateway.BeginGetResponse, request_gateway.EndGetResponse, null).ConfigureAwait(false);
                        Stream resStream_gateway = response_gateway.GetResponseStream();

                        StreamReader reader_gateway = new StreamReader(resStream_gateway);

                        gateway = reader_gateway.ReadToEnd();

                        log("Gateway choosed: " + gateway);
                        if (activated)
                        {

                            //Setting up interface config
                            setGateway(gateway);
                            //setDNS(interfaceName, "10.20.0.1,8.8.8.8");

                        }

                        break;
                    }
                    catch (WebException)
                    {

                        log("Impossible de contacter le serveur Rivnet");

                    }

                }

                if (!contacted)
                {

                    log("Aucun serveur Rivnet n'est accessible.");

                }

            }
            else {

                log("Rivnet server IP not configured");

            }
        	
        	trayMenu.Items[0].ToolTipText = "Interface: " + interfaceName + "\nMac: " + mac + "\nIp: " + localIP + "\nNetmask: " + netmask + "\nGw: " + gateway;
            
        }
        
        
        /// <summary>
        /// Set's a new Gateway address of the local machine
        /// </summary>
        /// <param name="gateway">The Gateway IP Address</param>
        /// <remarks>Requires a reference to the System.Management namespace</remarks>
        public void setGateway(string gateway)
        {
        	
            ManagementClass objMC = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection objMOC = objMC.GetInstances();

            foreach (ManagementObject objMO in objMOC)
            {
                if ((bool)objMO["IPEnabled"])
                {
                    
                    try
                    {
                        ManagementBaseObject setGateway;
                        ManagementBaseObject newGateway = objMO.GetMethodParameters("SetGateways");

                        newGateway["DefaultIPGateway"] = new string[] { gateway };
                        newGateway["GatewayCostMetric"] = new int[] { 1 };
                        
                        setGateway = objMO.InvokeMethod("SetGateways", newGateway, null);

                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }
        	
        }
        
        
        /// <summary>
        /// Set's the DNS Server of the local machine
        /// </summary>
        /// <param name="NIC">NIC address</param>
        /// <param name="DNS">DNS server address</param>
        /// <remarks>Requires a reference to the System.Management namespace</remarks>
        public void setDNS(string NIC, string DNS)
        {
            ManagementClass objMC = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection objMOC = objMC.GetInstances();

            foreach (ManagementObject objMO in objMOC)
            {
                if ((bool)objMO["IPEnabled"])
                {
                    // if you are using the System.Net.NetworkInformation.NetworkInterface you'll need to change this line to if (objMO["Caption"].ToString().Contains(NIC)) and pass in the Description property instead of the name 
                    if (objMO["Caption"].Equals(NIC))
                    {
                        try
                        {
                            ManagementBaseObject newDNS = objMO.GetMethodParameters("SetDNSServerSearchOrder");
                            newDNS["DNSServerSearchOrder"] = DNS.Split(',');
                            ManagementBaseObject setDNS = objMO.InvokeMethod("SetDNSServerSearchOrder", newDNS, null);
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                    }
                }
            }
        }
        
        private string tokenize(string s)
		{
        	
			if (s.Length == 0) {
				return "";
			}
        	
        	string res = "";
        	
        	int size = s.Length;
        	for (int i = 0; i < size-2; i+=2) {
        		res += s[i] + "" + s[i+1] + ":";
			}
			
        	res += s[size-2];
        	res += s[size-1];
        	
			return res;
			
		}


        
        
        
        private void onChangeInterface(object sender, EventArgs e)
        {
        	changeInterface(sender.ToString());
        }
        
        
        private void onActivate(object sender, EventArgs e)
        {
        	activate();
        }
        
        
        private void onDesactivate(object sender, EventArgs e)
        {
        	desactivate();
        }
        
        
        private void onRefresh(object sender, EventArgs e)
        {
        	rivnetRequesting = true;
        	refresh();
        }
        
                
        
        
        
        
        
        protected override void OnLoad(EventArgs e)
        {
            Visible       = false; // Hide form window.
            ShowInTaskbar = false; // Remove from taskbar.
 
            base.OnLoad(e);
        }
 
        private void OnExit(object sender, EventArgs e)
        {
            Application.Exit();
        }
 
        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // Release the icon resource.
                trayIcon.Dispose();
            }
 
            base.Dispose(isDisposing);
        }
        
        
        private Parser parse(string path)
		{

            try
			{
				
				Parser parser = new Parser(path);

				try
				{
				
					var a = parser.GetString(section, "activated") == "true" ? true : false;
					var b = parser.GetString(section, "interface");
					var c = parser.GetString(section, "rivnetIPs");
					return parser;
										
				}catch(KeyNotFoundException)
				{

                    string rivnetIPsRAW = "";

                    log("Key not found ");
					parser.SetString(section, "activated", activated ? "true": "false");
					parser.SetString(section, "interface", interfaceName);
					parser.SetString(section, "rivnetIPs", rivnetIPsRAW);
					
			    	parse(path);
					
				}
				
			}catch(FileNotFoundException)
			{

                log("File not found");
				StreamWriter fileW = new StreamWriter(path, true);
				fileW.WriteLine("[" + section + "]");
				fileW.Close();
			    
			    parse(path);
				
			}catch(ArgumentException)
			{
				
				log("File corrupted");
			   	File.WriteAllText(path, "[" + section + "]");
			    parse(path);
				
			}
				
			return null;
			
		}
        
        
        private void read()
        {
        	
			Parser parser = parse(path);

            activated = parser.GetString(section, "activated") == "true" ? true : false;
			interfaceName = parser.GetString(section, "interface");
			string rivnetIPsRAW = parser.GetString(section, "rivnetIPs");
            
            rivnetIPs = new List<string>(rivnetIPsRAW.Split(';'));

        }
        
        
        private void write()
        {
        	
			Parser parser = parse(path);

            string rivnetIPsRAW = "";

            foreach (string ip in rivnetIPs)
            {

                rivnetIPsRAW += ip + ";";

            }

            rivnetIPsRAW = rivnetIPsRAW.Trim(';');
            
            parser.SetString(section, "activated", activated ? "true": "false");
			parser.SetString(section, "interface", interfaceName);
			parser.SetString(section, "rivnetIPs", rivnetIPsRAW);
			
        }


        private void log(string logMessage)
        {
            File.AppendAllText("log.txt", "\r\n" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "  : " + logMessage);
        }

    }
}