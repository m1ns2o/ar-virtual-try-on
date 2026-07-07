using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ARCloset
{
    [Serializable]
    public struct MediaPipePoseLandmark
    {
        public float x;
        public float y;
        public float z;
        public float visibility;
        public float presence;
    }

    [Serializable]
    public sealed class MediaPipePosePacket
    {
        public int version;
        public double timestamp;
        public int frameWidth;
        public int frameHeight;
        public int displayRotationDegrees;
        public bool inputMirrored;
        public bool previewMirrored;
        public MediaPipePoseLandmark[] landmarks;
        public MediaPipePoseLandmark[] worldLandmarks;
    }

    public sealed class MediaPipePoseReceiver : MonoBehaviour
    {
        [SerializeField] private int listenPort = 5052;
        [SerializeField] private float staleAfterSeconds = 1.5f;

        private readonly object messageLock = new();
        private UdpClient client;
        private Thread receiveThread;
        private string pendingJson;
        private volatile bool running;
        private MediaPipePosePacket latestPacket;
        private string latestSource = "UDP";
        private float lastPacketTime;
        private int latestSequence;
        private int receivedMessages;
        private int parsedPackets;

        public int ListenPort => listenPort;
        public int LatestSequence => latestSequence;
        public string LatestSource => latestSource;
        public int ReceivedMessages => receivedMessages;
        public int ParsedPackets => parsedPackets;
        public float PacketAgeSeconds
        {
            get
            {
                ConsumePendingJson();
                return latestPacket == null ? float.PositiveInfinity : Time.realtimeSinceStartup - lastPacketTime;
            }
        }

        public bool HasFreshPose
        {
            get
            {
                ConsumePendingJson();
                return latestPacket?.landmarks != null && latestPacket.landmarks.Length >= 33 && PacketAgeSeconds <= staleAfterSeconds;
            }
        }

        private void OnEnable()
        {
            StartReceiver();
        }

        private void Update()
        {
            ConsumePendingJson();
        }

        private void OnDisable()
        {
            StopReceiver();
        }

        private void OnDestroy()
        {
            StopReceiver();
        }

        public bool TryGetLatestPacket(out MediaPipePosePacket packet)
        {
            ConsumePendingJson();
            packet = latestPacket;
            return HasFreshPose;
        }

        public void PushPacket(MediaPipePosePacket packet, string source = "Unity")
        {
            if (packet?.landmarks == null || packet.landmarks.Length < 33)
            {
                return;
            }

            latestPacket = packet;
            latestSource = source;
            lastPacketTime = Time.realtimeSinceStartup;
            latestSequence++;
            parsedPackets++;
        }

        private void ConsumePendingJson()
        {
            string json = null;

            lock (messageLock)
            {
                if (!string.IsNullOrEmpty(pendingJson))
                {
                    json = pendingJson;
                    pendingJson = null;
                }
            }

            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                MediaPipePosePacket packet = JsonUtility.FromJson<MediaPipePosePacket>(json);
                if (packet?.landmarks == null)
                {
                    return;
                }

                latestPacket = packet;
                latestSource = $"UDP:{listenPort}";
                lastPacketTime = Time.realtimeSinceStartup;
                latestSequence++;
                parsedPackets++;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"MediaPipe pose packet parse failed: {exception.Message}");
            }
        }

        private void StartReceiver()
        {
            if (running)
            {
                return;
            }

            try
            {
                client = new UdpClient(listenPort);
                running = true;
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "MediaPipe Pose UDP Receiver",
                };
                receiveThread.Start();
            }
            catch (Exception exception)
            {
                running = false;
                Debug.LogError($"Could not start MediaPipe UDP receiver on port {listenPort}: {exception.Message}");
            }
        }

        private void StopReceiver()
        {
            running = false;

            try
            {
                client?.Close();
            }
            catch
            {
            }

            client = null;
            receiveThread = null;
        }

        private void ReceiveLoop()
        {
            IPEndPoint endpoint = new(IPAddress.Any, listenPort);

            while (running)
            {
                try
                {
                    byte[] bytes = client.Receive(ref endpoint);
                    string json = Encoding.UTF8.GetString(bytes);
                    Interlocked.Increment(ref receivedMessages);

                    lock (messageLock)
                    {
                        pendingJson = json;
                    }
                }
                catch (SocketException)
                {
                    if (running)
                    {
                        Thread.Sleep(20);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    if (running)
                    {
                        Debug.LogWarning($"MediaPipe UDP receive failed: {exception.Message}");
                        Thread.Sleep(50);
                    }
                }
            }
        }
    }
}
