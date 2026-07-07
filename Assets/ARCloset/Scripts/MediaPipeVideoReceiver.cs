using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace ARCloset
{
    public sealed class MediaPipeVideoReceiver : MonoBehaviour
    {
        [SerializeField] private int listenPort = 5053;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private float staleAfterSeconds = 2.0f;
        [SerializeField] private int maxFrameBytes = 2 * 1024 * 1024;

        private readonly object frameLock = new();
        private TcpListener listener;
        private Thread listenThread;
        private byte[] pendingFrame;
        private Texture2D texture;
        private volatile bool running;
        private int receivedFrames;
        private int appliedFrames;
        private float lastFrameTime;

        public int ListenPort => listenPort;
        public int ReceivedFrames => receivedFrames;
        public int AppliedFrames => appliedFrames;
        public bool HasFreshFrame => appliedFrames > 0 && Time.realtimeSinceStartup - lastFrameTime <= staleAfterSeconds;

        private void OnEnable()
        {
            StartReceiver();
        }

        private void Update()
        {
            ApplyPendingFrame();
        }

        private void OnDisable()
        {
            StopReceiver();
        }

        private void OnDestroy()
        {
            StopReceiver();
        }

        public void ApplyPendingFrame()
        {
            byte[] frame = null;

            lock (frameLock)
            {
                if (pendingFrame != null)
                {
                    frame = pendingFrame;
                    pendingFrame = null;
                }
            }

            if (frame == null || frame.Length == 0)
            {
                return;
            }

            texture ??= new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(frame, false))
            {
                return;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            if (targetRenderer != null)
            {
                targetRenderer.sharedMaterial.mainTexture = texture;
            }

            lastFrameTime = Time.realtimeSinceStartup;
            appliedFrames++;
        }

        private void StartReceiver()
        {
            if (running)
            {
                return;
            }

            try
            {
                listener = new TcpListener(IPAddress.Loopback, listenPort);
                listener.Start();
                running = true;
                listenThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "MediaPipe Video TCP Receiver",
                };
                listenThread.Start();
            }
            catch (Exception exception)
            {
                running = false;
                Debug.LogError($"Could not start MediaPipe video receiver on port {listenPort}: {exception.Message}");
            }
        }

        private void StopReceiver()
        {
            running = false;

            try
            {
                listener?.Stop();
            }
            catch
            {
            }

            listener = null;
            listenThread = null;
        }

        private void ListenLoop()
        {
            while (running)
            {
                TcpClient client = null;

                try
                {
                    client = listener.AcceptTcpClient();
                    client.NoDelay = true;
                    using NetworkStream stream = client.GetStream();

                    while (running && client.Connected)
                    {
                        int length = ReadFrameLength(stream);
                        if (length <= 0 || length > maxFrameBytes)
                        {
                            break;
                        }

                        byte[] frame = ReadExactly(stream, length);
                        if (frame == null)
                        {
                            break;
                        }

                        lock (frameLock)
                        {
                            pendingFrame = frame;
                        }

                        Interlocked.Increment(ref receivedFrames);
                    }
                }
                catch (SocketException)
                {
                    if (running)
                    {
                        Thread.Sleep(50);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (IOException)
                {
                    if (running)
                    {
                        Thread.Sleep(50);
                    }
                }
                catch (Exception exception)
                {
                    if (running)
                    {
                        Debug.LogWarning($"MediaPipe video receive failed: {exception.Message}");
                        Thread.Sleep(100);
                    }
                }
                finally
                {
                    try
                    {
                        client?.Close();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static int ReadFrameLength(NetworkStream stream)
        {
            byte[] lengthBytes = ReadExactly(stream, 4);
            if (lengthBytes == null)
            {
                return -1;
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }

            return BitConverter.ToInt32(lengthBytes, 0);
        }

        private static byte[] ReadExactly(NetworkStream stream, int length)
        {
            byte[] buffer = new byte[length];
            int offset = 0;

            while (offset < length)
            {
                int read = stream.Read(buffer, offset, length - offset);
                if (read <= 0)
                {
                    return null;
                }

                offset += read;
            }

            return buffer;
        }
    }
}
