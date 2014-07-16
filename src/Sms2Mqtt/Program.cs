using System.Diagnostics;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace Sms2Mqtt
{
    using uPLibrary.Networking.M2Mqtt;
    using System;

    class Program
    {
        private static bool _keepTrying = true;
        private const string SmscmdAppName = "<insert app name>";
        private const string SmscmdMqttUsername = "<insert mqtt usernamee>";
        private const string SmscmdMqttPassword = "<insert mqtt password>";
        private static readonly string MqttSubTopic = string.Format("{0}/cmds", SmscmdAppName);
        private static readonly string MqttPubTopic = string.Format("{0}/reply", SmscmdAppName);

        static void Main(string[] args)
        {
            Console.CancelKeyPress += (s, e) => _keepTrying = false;

            Console.WriteLine("Press CTRL+C to quit.");

            var mqttClient = new MqttClient("q.smscmd.net" /* host */, 1883 /* port */, false /* secure */, null /* cert */);

            mqttClient.MqttMsgPublishReceived += (s, e) =>
            {
                var rawCmd = Encoding.UTF8.GetString(e.Message);
                var command = JsonConvert.DeserializeObject<dynamic>(rawCmd);

                Console.WriteLine("Received command [{0}] with body \"{1}\" on {2} from {3}", 
                    command.cmdId, command.body, command.date, command.src);

                // You can reply to this message by including the cmdId
                // /reply is special, this is how we send things to SMSCMD
                var reply = JsonConvert.SerializeObject(new {cmdId = command.cmdId, body = command.body});
                var replyBytes = Encoding.UTF8.GetBytes(reply);
                var puback = mqttClient.Publish(MqttPubTopic, replyBytes, MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false /* retain */);
                Debug.WriteLine("Puback from sending {0}: {1}", reply, puback);
            };


            Action connect = () =>
            {
                while (_keepTrying)
                {
                    try
                    {
                        Console.WriteLine("Trying to connect...");
                        var conack = mqttClient.Connect(SmscmdAppName, SmscmdMqttUsername, SmscmdMqttPassword);
                        Debug.WriteLine("Conack: {0}", (int)conack);

                        // /cmds is special. This is how messages come from SMSCMD
                        var suback = mqttClient.Subscribe(new[] { MqttSubTopic }, new[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
                        Debug.WriteLine("Suback: {0}", suback);
                        
                        Console.WriteLine("Connected!");
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("Error connecting. {0}", e);
                        Thread.Sleep(10000);
                    }
                }
            };

            mqttClient.MqttMsgDisconnected += (s, e) =>
            {
                Console.WriteLine("Woops, disconnected.");
                connect();
            };


            connect();

            while (_keepTrying)
            {
                Thread.Sleep(1000);
            }
        }
    }
}
