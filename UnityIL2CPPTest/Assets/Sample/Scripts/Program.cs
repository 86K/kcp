using System;
using System.Buffers;
using System.Net.Sockets.Kcp;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Random = System.Random;

namespace TestKCP
{
    public class Handle : IKcpCallback
    {
        //public void Output(ReadOnlySpan<byte> buffer)
        //{
        //    var frag = new byte[buffer.Length];
        //    buffer.CopyTo(frag);
        //    Out(frag);
        //}

        public Action<Memory<byte>> Out;
        public Action<byte[]> Recv;
        public void Receive(byte[] buffer)
        {
            Recv(buffer);
        }

        public IMemoryOwner<byte> RentBuffer(int lenght)
        {
            return null;
        }

        public void Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            using (buffer)
            {
                Out(buffer.Memory.Slice(0, avalidLength));
            }
        }
    }

    public class Program
    {
        static string ShowThread => $"  ThreadID[{Thread.CurrentThread.ManagedThreadId}]";
        
        public static void Main(string[] args)
        {
            Debug.Log(ShowThread);
            // Console.WriteLine(ShowThread);
            Random random = new Random();

            var handle1 = new Handle();
            var handle2 = new Handle();

            const int conv = 123;
            var kcp1 = new Kcp(conv, handle1);
            var kcp2 = new Kcp(conv, handle2);

            kcp1.NoDelay(1, 10, 2, 1);//fast
            kcp1.WndSize(64, 64);
            kcp1.SetMtu(512);

            kcp2.NoDelay(1, 10, 2, 1);//fast
            kcp2.WndSize(64, 64);
            kcp2.SetMtu(512);

            var sendbyte = Encoding.ASCII.GetBytes(TestClass.message);

            handle1.Out += buffer =>
            {
                var next = random.Next(100);
                if (next >= 15)///随机丢包
                {
                    //Console.WriteLine($"11------Thread[{Thread.CurrentThread.ManagedThreadId}]");
                    Task.Run(() =>
                    {
                        //Console.WriteLine($"12------Thread[{Thread.CurrentThread.ManagedThreadId}]");
                        kcp2.Input(buffer.Span);
                    });

                }
                else
                {
                    //Console.WriteLine("Send miss");
                    Debug.Log("Send miss");
                }
            };

            handle2.Out += buffer =>
            {
                var next = random.Next(100);
                if (next >= 0)///随机丢包
                {
                    Task.Run(() =>
                    {
                        kcp1.Input(buffer.Span);
                    });
                }
                else
                {
                    // Console.WriteLine("Resp miss");
                    Debug.Log("Resp miss");
                }
            };
            int count = 0;

            handle1.Recv += buffer =>
            {
                var str = Encoding.ASCII.GetString(buffer);
                count++;
                if (TestClass.message == str)
                {
                    kcptest.Log1($"kcp  echo----{count}");
                }
                var res = kcp1.Send(buffer);
                if (res != 0)
                {
                    kcptest.Log1($"kcp send error");
                }
            };

            int recvCount = 0;

            handle2.Recv += buffer =>
            {
                recvCount++;
                kcptest.Log2($"kcp2 recv----{recvCount}");
                var res = kcp2.Send(buffer);
                if (res != 0)
                {
                    kcptest.Log2($"kcp send error");
                }
            };

            Task.Run(async () =>
            {
                try
                {
                    int updateCount = 0;
                    while (true)
                    {
                        kcp1.Update(DateTime.UtcNow);

                        int len;
                        while ((len = kcp1.PeekSize()) > 0)
                        {
                            var buffer = new byte[len];
                            if (kcp1.Recv(buffer) >= 0)
                            {
                                handle1.Receive(buffer);
                            }
                        }

                        await Task.Delay(5);
                        updateCount++;
                        if (updateCount % 1000 == 0)
                        {
                            // Console.WriteLine($"KCP1 ALIVE {updateCount}----{ShowThread}");
                            Debug.Log($"KCP1 ALIVE {updateCount}----{ShowThread}");
                        }
                    }
                }
                catch (Exception e)
                {
                    // Console.WriteLine(e);
                    Debug.Log(e);
                }

            });

            Task.Run(async () =>
            {
                try
                {
                    int updateCount = 0;
                    while (true)
                    {
                        kcp2.Update(DateTime.UtcNow);

                        //var utcNow = DateTime.UtcNow;
                        //var res = kcp2.Check(utcNow);

                        int len;
                        do
                        {
                            var (buffer, avalidSzie) = kcp2.TryRecv();
                            len = avalidSzie;
                            if (buffer != null)
                            {
                                var temp = new byte[len];
                                buffer.Memory.Span.Slice(0, len).CopyTo(temp);
                                handle2.Receive(temp);
                            }
                        } while (len > 0);

                        await Task.Delay(5);
                        updateCount++;
                        if (updateCount % 1000 == 0)
                        {
                            // Console.WriteLine($"KCP2 ALIVE {updateCount}----{ShowThread}");
                            Debug.Log($"KCP2 ALIVE {updateCount}----{ShowThread}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    Debug.Log(e);
                }
            });

            kcp1.Send(sendbyte);

            
        }
    }
}
