﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Runtime.InteropServices;

namespace Curry_Client
{
    public partial class loginform : Form
    {
        private bool firstnamecheck, lastnamecheck, passwordcheck, curriculumcheck = false;
        public static byte[] receivedpacket = new byte[256];
        Dictionary<String, String> masterServerList = new Dictionary<String, String>();

        private const int port = 32320;
        // ManualResetEvent instances signal completion.
        private static ManualResetEvent connectDone =
            new ManualResetEvent(false);
        private static ManualResetEvent sendDone =
            new ManualResetEvent(false);
        private static ManualResetEvent receiveDone =
            new ManualResetEvent(false);

        // The response from the remote device.
        private static String response = String.Empty;


        private byte[] verificationcode = new byte[3];
        public loginform()
        {
            InitializeComponent();
        }

        private Form1 mainForm = null;
        public loginform(Form callingForm)
        {
            mainForm = callingForm as Form1; 
            InitializeComponent();
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            
        }

        private void verify()
        {
            this.mainForm.logincode = verificationcode;
        }

        private byte[] getLoginProtocol()
        {
            byte[] packet = new byte[78];
            //Starting byteflag 1 is the login protocol (client to server)
            //Next 20 bytes is first name
            //Next 20 bytes is last name
            //Next 20 bytes is password
            //Next 5 bytes is <EOP> closing flag
            //Total bytes in packet: 65
            packet[0] = 1;
            byte[] firstname = Encoding.ASCII.GetBytes(firstnamebox.Text);
            firstname.CopyTo(packet, 1);
            byte[] lastname = Encoding.ASCII.GetBytes(lastnamebox.Text);
            lastname.CopyTo(packet, 21);
            byte[] password = Encoding.ASCII.GetBytes(passwordbox.Text);
            password.CopyTo(packet, 41);
            byte[] closing = Encoding.ASCII.GetBytes("<EOF>");
            closing.CopyTo(packet, 61);

            return packet;
        }

        //IP_AD is the IP address of the server
        private void connect(string IP_AD)
        {
            try
            {
                //init
                IPAddress ipAddress = IPAddress.Parse(IP_AD);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);
                Socket client = new Socket(AddressFamily.InterNetwork,
                    SocketType.Stream, ProtocolType.Tcp);

                //Connect through the remote socket (endpoint)
                client.BeginConnect(remoteEP,
                    new AsyncCallback(ConnectCallback), client);

                // Send test data to the remote device.
                SendBytes(client, getLoginProtocol());
                sendDone.WaitOne();

                // Receive the response from the remote device.
                Receive(client);
                receiveDone.WaitOne();

                // Write the response to the console.
                //Response: First byte (0) is verification that the packet is a type 1 packet
                //Second byte is pass (0) or fail(1) for authorization
                //Next (3) bytes are authoization bytes (512 bit authorization)
                if (receivedpacket[0] == 1)
                {
                    if (receivedpacket[1] == 0)
                    {
                        //Wrong name or password entered by user
                    }
                    else
                    {
                        //Verified that login is good
                        verificationcode[0] = receivedpacket[2];
                        verificationcode[1] = receivedpacket[3];
                        verificationcode[2] = receivedpacket[4];
                    }
                } else{
                    Console.WriteLine("A fatal error has occurred: Incorrect packet type returned by server to login verification protocol");
                }
                Console.WriteLine("Response received : " + Encoding.ASCII.GetString(receivedpacket, 0, receivedpacket.Length));

                // Release the socket.
                client.Shutdown(SocketShutdown.Both);
                client.Close();


            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.
                client.EndConnect(ar);

                Console.WriteLine("Socket connected to " + client.RemoteEndPoint.ToString());

                // Signal that the connection has been made.
                connectDone.Set();
            }
            catch (Exception e)
            {
                String p = e.ToString();
                Console.WriteLine(p);
            }
        }

        private static void Receive(Socket client)
        {
            try
            {
                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = client;

                // Begin receiving the data from the remote device.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket 
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.

                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    // Get the rest of the data.
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    // All the data has arrived; put it in response.
                    if (state.sb.Length > 1)
                    {
                        response = state.sb.ToString();
                        receivedpacket = state.buffer;
                    }
                    // Signal that all bytes have been received.
                    receiveDone.Set();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void Send(Socket client, String data)
        {
            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.
            client.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), client);
        }

        private static void SendBytes(Socket client, byte[] data)
        {
            // Begin sending the data to the remote device.
            client.BeginSend(data, 0, data.Length, 0,
                new AsyncCallback(SendCallback), client);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);
                Console.WriteLine("Sent " + bytesSent + " bytes to server.");

                // Signal that all bytes have been sent.
                sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            connect("127.0.0.1");
        }

        private void lastnamebox_Enter(object sender, EventArgs e)
        {
            if (!lastnamecheck)
            {
                lastnamebox.Text = "";
                lastnamebox.ForeColor = Color.Black;
                lastnamecheck = true;
            }
        }

        private void passwordbox_Enter(object sender, EventArgs e)
        {
            if (!passwordcheck)
            {
                passwordbox.Text = "";
                passwordbox.ForeColor = Color.Black;
                passwordbox.UseSystemPasswordChar = true;
                passwordcheck = true;
            }
        }

        private void comboBox1_Enter(object sender, EventArgs e)
        {
            if (!curriculumcheck)
            {
                comboBox1.Text = "";
                comboBox1.ForeColor = Color.Black;
                curriculumcheck = true;
            }
        }   

        private void loginform_Load(object sender, EventArgs e)
        {
            Console.WriteLine("Client Launch");

            //Write dictionary of servers to connect to
            //Pull from master server (list of servers to connect to)
            masterServerList.Add("Localhost", "127.0.0.1");
            /*
            foreach (String s in masterServerList.Keys)
            {
                comboBox1.Items.Add(s);
            }
            */
            comboBox1.Items.Add("Localhost");

            if (!comboBox1.Items.Contains("Curriculum"))
            {
                comboBox1.Items.Add("Curriculum");
            }

            comboBox1.Text = "Curriculum";
        }

        private void firstnamebox_TextChanged(object sender, EventArgs e)
        {
            if (!firstnamecheck)
            {
                firstnamebox.Text = "";
                firstnamebox.ForeColor = Color.Black;
                firstnamecheck = true;
            }
        }

        private void firstnamebox_Click(object sender, EventArgs e)
        {
            if (!firstnamecheck)
            {
                firstnamebox.Text = "";
                firstnamebox.ForeColor = Color.Black;
                firstnamecheck = true;
            }
        }

        private void Enter_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedItem.ToString() != null && comboBox1.SelectedItem.ToString() != "Curriculum" && comboBox1.SelectedItem.ToString() != "")  
            {
                Console.WriteLine(comboBox1.SelectedItem.ToString());
                Console.WriteLine(masterServerList[comboBox1.SelectedItem.ToString()]);
                connect(masterServerList[comboBox1.SelectedItem.ToString()]);
            }
            else
            {
                comboBox1.BackColor = Color.Red;
            }
        }

        private void firstnamebox_Leave(object sender, EventArgs e)
        {
            if (firstnamebox.Text == "")
            {
                firstnamebox.Text = "First Name";
                firstnamecheck = false;
                firstnamebox.ForeColor = SystemColors.InactiveCaption;
            }
        }

        private void lastnamebox_Leave(object sender, EventArgs e)
        {
            if (lastnamebox.Text == "")
            {
                lastnamebox.Text = "Last Name";
                lastnamecheck = false;
                lastnamebox.ForeColor = SystemColors.InactiveCaption;
            }
        }

        private void comboBox1_DropDown(object sender, EventArgs e)
        {
            if (comboBox1.Items.Contains("Curriculum"))
                comboBox1.Items.Remove("Curriculum");
        }

        private void passwordbox_Leave(object sender, EventArgs e)
        {
            if (passwordbox.Text == "")
            {
                passwordbox.Text = "Password";
                passwordcheck = false;
                passwordbox.UseSystemPasswordChar = false;
                passwordbox.ForeColor = SystemColors.InactiveCaption;
            }
        }
    }

    // State object for receiving data from remote device.
    public class StateObject
    {
        // Client socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 256;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
        // Received data string.
        public StringBuilder sb = new StringBuilder();
    }
}
