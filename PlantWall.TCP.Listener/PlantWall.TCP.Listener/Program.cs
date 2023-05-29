using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlantWall.TCP.Listener
{
    class Program
    {
        //创建一个和客户端通信的套接字
        static Socket serverSocket = null;
        //定义一个集合，存储客户端信息
        static List<Socket> Clients = new List<Socket>();
        //数据库连接字符串;
        public static string connectString = "Data Source=127.0.0.1;Initial Catalog = PlantWallDB; Persist Security Info=True;User ID = sa; Password=123th123";
        static void Main(string[] args)
        {
            //端口号（用来监听的）
            int port = 1885;
            IPAddress ipAdress = IPAddress.Parse("192.168.43.205");
            //将IP地址和端口号绑定到网络节点point上  
            IPEndPoint ipEndPoint = new IPEndPoint(ipAdress, port);
            //定义一个套接字用于监听客户端发来的消息，包含三个参数（IP4寻址协议，流式连接，Tcp协议）  
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //监听绑定的网络节点  
            serverSocket.Bind(ipEndPoint);
            //将套接字的监听队列长度限制为20  
            serverSocket.Listen(20);
            //负责监听客户端的线程:创建一个监听线程  
            Thread threadwatch = new Thread(WatchConnecting);
            //将窗体线程设置为与后台同步，随着主线程结束而结束  
            threadwatch.IsBackground = true;
            //启动线程     
            threadwatch.Start();
            Console.WriteLine("TCP服务端开启监听......");
            while (true)
            {
                var inputString = Console.ReadLine().ToLower().Trim();

                if (inputString == "exit")
                {
                    serverSocket.Close();
                    Console.WriteLine("TCP服务已停止！");
                    break;
                }
                else if (inputString == "clients")
                {
                    foreach (var item in Clients)
                    {
                        Console.WriteLine($"客户端标识：{item.RemoteEndPoint}");
                    }
                }
                else
                {
                    Console.WriteLine($"命令[{inputString}]无效！");
                }
            }
        }

        /// <summary>
        /// 监听客户端发来的请求 
        /// </summary>
        static void WatchConnecting()
        {
            Socket clientSocket = null;
            //持续不断监听客户端发来的请求     
            while (true)
            {
                // string cID;
                try
                {
                    clientSocket = serverSocket.Accept();
                }
                catch (Exception ex)
                {
                    //提示套接字监听异常     
                    Console.WriteLine(ex.Message);
                    break;
                }

                Clients.Add(clientSocket);
                //显示与客户端连接情况
                Console.WriteLine($"客户端[{clientSocket.RemoteEndPoint}]建立连接成功！ 在线客户端数量：[{Clients.Count}]");

                ////提示客户端连接成功
                //clientSocket.Send(Encoding.UTF8.GetBytes("READ"));

                //创建一个通信线程      
                Thread thread = new Thread(MessageReceive);
                //设置为后台线程，随着主线程退出而退出 
                thread.IsBackground = true;
                //启动线程
                thread.Start(clientSocket);
            }
        }

        /// <summary>
        /// 处理收到的消息
        /// </summary>
        /// <param name="socketclientpara"></param>
        static void MessageReceive(object socketclientpara)
        {
            Socket socketClient = socketclientpara as Socket;
            string sql;
            while (true)
            {
                try
                {
                    byte[] arrServerRecMsg = new byte[1024 * 1024];
                    int length = socketClient.Receive(arrServerRecMsg);
                    if (length == 0)
                    {
                        Console.WriteLine($"客户端[{socketClient.RemoteEndPoint}]已断开连接！ 在线客户端数量：[{Clients.Count - 1}]");
                        Clients.Remove(socketClient);
                        socketClient.Close();
                        break;
                    }
                    else
                    {
                        //将服务端接受到的字节数组转换为字符串     
                        string strSRecMsg = Encoding.UTF8.GetString(arrServerRecMsg, 0, length);
                        //将粘包的数据分割，因为硬件端尝试加长度包头失败，因此放弃使用包头方法，改为特殊字符结尾处理粘包
                        string[] ArraySRecMsg = strSRecMsg.Split("*");//将粘包的内容以*分割成字符串数组
                        for (int x = 0; x < ArraySRecMsg.Length; x++)
                        {
                            string singleRecMsg = ArraySRecMsg[x];
                            if (singleRecMsg == "") { }
                            //转发发布端的消息
                            else if (singleRecMsg.StartsWith("pub "))
                            {
                                string pubSendMsg = singleRecMsg.Remove(0, 4);
                                if (Clients.Count > 1)
                                {
                                    Clients[1].Send(Encoding.UTF8.GetBytes(pubSendMsg));
                                }
                                Console.WriteLine($"发布端[{Clients[0].RemoteEndPoint}]消息:{singleRecMsg}  长度:[{singleRecMsg.Length}]");
                            }
                            else
                            {
                                if (Clients.Count > 1)
                                {
                                    //服务端处理硬件端的消息
                                    if (singleRecMsg.Length != 0)
                                    {
                                        string[] msgArray = singleRecMsg.Split(' ');
                                        if (msgArray.Length == 4)
                                        {
                                            string instruct = new string(msgArray[0]);
                                            string parameter = new string(msgArray[1]);
                                            string date = new string(msgArray[2]);
                                            string account = new string(msgArray[3]);
                                            if (instruct.Equals("switch", StringComparison.OrdinalIgnoreCase))
                                            {
                                                try
                                                {
                                                    using (SqlConnection sqlConnection = new SqlConnection(connectString))
                                                    {
                                                        sqlConnection.Open();
                                                        sql = $"update [PlantWallDB].[dbo].[Instruction] set {parameter}={Convert.ToInt32(date)} where Account='{account}'";
                                                        using (SqlCommand cmd = new SqlCommand(sql, sqlConnection))
                                                        {
                                                            cmd.ExecuteNonQuery();
                                                        }
                                                    }
                                                    Console.WriteLine("指令完成");
                                                }
                                                catch (Exception ex)
                                                {
                                                    //Console.WriteLine("指令包含的传递数据不合法");
                                                    Console.WriteLine(ex.Message);
                                                }
                                            }
                                            else if (instruct.Equals("data", StringComparison.OrdinalIgnoreCase))
                                            {
                                                try
                                                {
                                                    using (SqlConnection sqlConnection = new SqlConnection(connectString))
                                                    {
                                                        sqlConnection.Open();
                                                        string[] instructiondate = new string[7];
                                                        for (int i = 0; i < instructiondate.Length; i++)
                                                        {
                                                            instructiondate[i] = parameter + (i + 1);
                                                        }
                                                        sql = $"update [PlantWallDB].[dbo].[SensorData] set {instructiondate[6]} = {instructiondate[5]},{instructiondate[5]} = {instructiondate[4]},{instructiondate[4]} = {instructiondate[3]},{instructiondate[3]} = {instructiondate[2]},{instructiondate[2]} = {instructiondate[1]},{instructiondate[1]} = {instructiondate[0]},{instructiondate[0]} = {Convert.ToDouble(date)} where Account = '{account}'";
                                                        using (SqlCommand cmd = new SqlCommand(sql, sqlConnection))
                                                        {
                                                            cmd.ExecuteNonQuery();
                                                        }
                                                        double temperature = 0, humidity = 0, carbondioxide = 0, oxygen = 0, saturation = 0, watervapour = 0;
                                                        sql = $"select Temperature1,Humidity1,CarbonDioxide1 from[PlantWallDB].[dbo].[SensorData] where account='{account}'";
                                                        using (SqlCommand cmd = new SqlCommand(sql, sqlConnection))
                                                        {
                                                            using (SqlDataReader reader = cmd.ExecuteReader())
                                                            {
                                                                while (reader.Read())
                                                                {
                                                                    temperature = reader.GetDouble(0);
                                                                    humidity = reader.GetDouble(1);
                                                                    carbondioxide = reader.GetDouble(2);
                                                                }
                                                            }
                                                        }
                                                        if (temperature <= -20)
                                                            saturation = 0.9;
                                                        else if (-20 < temperature && temperature <= -10)
                                                            saturation = 2.3;
                                                        else if (-10 < temperature && temperature <= 0)
                                                            saturation = 4.85;
                                                        else if (0 < temperature && temperature <= 10)
                                                            saturation = 9.35;
                                                        else if (10 < temperature && temperature <= 20)
                                                            saturation = 17.3;
                                                        else if (20 < temperature && temperature <= 30)
                                                            saturation = 30.3;
                                                        else if (30 < temperature && temperature <= 40)
                                                            saturation = 51;
                                                        else
                                                            saturation = 82.1;
                                                        watervapour = humidity * (0.0003 * saturation * saturation * saturation + 0.0105 * saturation * saturation + 0.3034 * saturation + 4.8083);
                                                        oxygen = Math.Round(carbondioxide * 675.4838709677419, 2);
                                                        sql = $"update [PlantWallDB].[dbo].[SensorData] set watervapour7 = watervapour6,watervapour6 = watervapour5,watervapour5 = watervapour4,watervapour4 = watervapour3,watervapour3 = watervapour2,watervapour2 = watervapour1,watervapour1 = {watervapour},Oxygen7 = Oxygen6,Oxygen6 = Oxygen5,Oxygen5 = Oxygen4,Oxygen4 = Oxygen3,Oxygen3 = Oxygen2,Oxygen2 = Oxygen1,Oxygen1 = {oxygen} where Account = '{account}'";
                                                        using (SqlCommand cmd = new SqlCommand(sql, sqlConnection))
                                                        {
                                                            cmd.ExecuteNonQuery();
                                                        }
                                                    }
                                                    Console.WriteLine("指令完成");
                                                }
                                                catch (Exception ex)
                                                {
                                                    //Console.WriteLine("指令包含的传递数据不合法"); 
                                                    Console.WriteLine(ex.Message);
                                                }

                                            }
                                            else if (instruct.Equals("set", StringComparison.OrdinalIgnoreCase))
                                            {
                                                try
                                                {

                                                    using (SqlConnection sqlConnection = new SqlConnection(connectString))
                                                    {
                                                        sqlConnection.Open();
                                                        sql = $"update [PlantWallDB].[dbo].[AddElement] set {parameter} ={Convert.ToInt32(date)} where Account='{account}'";
                                                        using (SqlCommand cmd = new SqlCommand(sql, sqlConnection))
                                                        {
                                                            cmd.ExecuteNonQuery();
                                                        }
                                                    }
                                                    Console.WriteLine("指令完成");
                                                }
                                                catch (Exception ex)
                                                {
                                                    Console.WriteLine(ex.Message);
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine($"来自客户端[{Clients[1].RemoteEndPoint}]的数据:{singleRecMsg}  数据长度:{singleRecMsg.Length} 无特殊执行");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine($"来自客户端[{Clients[1].RemoteEndPoint}]的数据:{singleRecMsg}  数据长度:{singleRecMsg.Length} 无特殊执行");
                                        }
                                    }
                                    Console.WriteLine($"硬件端[{Clients[1].RemoteEndPoint}]消息:{singleRecMsg}  长度:[{singleRecMsg.Length}]");
                                }
                            }
                        }
                        arrServerRecMsg.DefaultIfEmpty();
                    }
                }
                catch (Exception ex)
                {
                    //提示套接字监听异常  
                    Console.WriteLine($"处理过程异常：{ex.Message} 客户端[{socketClient.RemoteEndPoint}]已断开连接！ 在线客户端数量：[{Clients.Count - 1}]");
                    Clients.Remove(socketClient);
                    socketClient.Close();
                    break;
                }
            }
        }
    }
}