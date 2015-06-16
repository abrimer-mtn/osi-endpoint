using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Apache.NMS;
using Apache.NMS.Util;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Net.Sockets;

using System.IO.Compression;

namespace OsiSignalTestHarness
{
    public partial class SignalTestHarness : Form
    {
        protected static AutoResetEvent semaphore = new AutoResetEvent(false);
        protected static ITextMessage message = null;
        protected static TimeSpan receiveTimeout = TimeSpan.FromSeconds(10);

        private static string TOPIC_NAME = "topic://osi-signals";
        private static string BUS_FQDN = "devadmin.pearlnet.net";
        private static int BUS_PORT = 61616;

        private static string BusUri
        {
            get { return string.Format("activemq:tcp://{0}:{1}", BUS_FQDN, BUS_PORT); }
        }

        protected System.Timers.Timer busWatchdog;

        //Uri connecturi = new Uri("activemq:tcp://devadmin.pearlnet.net:61616");
        Uri connecturi = new Uri(BusUri);

        IConnectionFactory factory;

        IConnection connection;
        ISession session;

        IDestination destination;

        IMessageConsumer consumer;
        //IMessageProducer producer;

        bool connected = false;

        public SignalTestHarness()
        {
            InitializeComponent();

            busWatchdog = new System.Timers.Timer(5000);

            busWatchdog.Elapsed += new System.Timers.ElapsedEventHandler(CheckServiceBusStatus);

            //busWatchdog.Start();

            this.AttachServiceBus();
        }

        void CheckServiceBusStatus(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (connected)
                return;

            if (BusIsAvailable() == false)
                return;

            (sender as System.Timers.Timer).Stop();
                        
            if (AttachServiceBus(false))
            {
                IMessage msg;

                try
                {
                    while ((msg = consumer.Receive(TimeSpan.FromMilliseconds(1000))) != null)
                    {
                        OnMessage(msg);
                    }
                }
                catch
                {
                    string error = string.Empty;
                }

                ActivateConnectionListeners();
            }
            
        }

        private static void OnMessage(IMessage receivedMsg)
        {
            message = receivedMsg as ITextMessage;

            if (string.Empty == message.Text)
                return;

            string docname = message.Properties.Contains("destfilename") ?
                message.Properties["destfilename"].ToString() :
                Guid.NewGuid().ToString();

            try
            {
                XDocument doc = XDocument.Parse(message.Text, LoadOptions.PreserveWhitespace);

                doc.Save(string.Format("D:\\JsonEdit\\scc\\{0}.xml", docname));
            }
            catch (Exception)
            {
            }
        }

        private void ActivateConnectionListeners()
        {
            connection.ExceptionListener += new ExceptionListener(connection_ExceptionListener);
            consumer.Listener += new MessageListener(OnMessage);
        }

        private void DeactivateConnectionListeners()
        {
            connection.ExceptionListener -= connection_ExceptionListener;
            consumer.Listener -= OnMessage;
        }

        private void DetachServiceBus()
        {
            if (connected == false)
                return;

            connected = false;

            consumer.Dispose();
            session.Dispose();
            connection.Dispose();

            consumer = null;
            session = null;
            connection = null;
        }

        private bool AttachServiceBus(bool addListener = true)
        {
            string andy = string.Empty;

            bool retVal = true;

            try
            {
                if (connected || BusIsAvailable() == false)
                    return false;

                connection = NMSConnectionFactory.CreateConnectionFactory(connecturi).CreateConnection("mroot", "password");
                session = connection.CreateSession();

                destination = SessionUtil.GetDestination(session, TOPIC_NAME);

                connection.Start();

                consumer = session.CreateConsumer(destination);
                //producer = session.CreateProducer(destination);

                //producer.Persistent = true;
                //producer.RequestTimeout = receiveTimeout;

                if (addListener)
                {
                    ActivateConnectionListeners();
                }

                connected = true;
            }
            catch (Exception ex)
            {
                DetachServiceBus();

                retVal = false;
            }

            return retVal;
        }

        void connection_ExceptionListener(Exception exception)
        {
            DetachServiceBus();          
        }

        bool BusIsAvailable()
        {
            bool retVal = true;

            using (TcpClient bus = new TcpClient())
            {

                try
                {
                    bus.Connect(BUS_FQDN, BUS_PORT);

                    bus.Close();
                }
                catch
                {
                    retVal = false;

                    DetachServiceBus();
                }
            }

            return retVal;
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if(connected)
            {
                // Send a message
                ITextMessage request = session.CreateTextMessage("Hello World!");
                request.NMSCorrelationID = "abc";
                request.Properties["NMSXGroupID"] = "cheese";
                request.Properties["myHeader"] = "Cheddar";

                //producer.Send(request);
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if ((sender as CheckBox).Checked)
            {
                busWatchdog.Start();
            }
            else
            {
                busWatchdog.Stop();
            }
        }

        private void UnpackSignals()
        {

        }

    }
}
