using System;
using System.Collections.Generic;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.Utilities.Assets
{
    /// <summary>
    /// The different types of assets in Second Life
    /// </summary>
    public enum AssetType
    {
        /// <summary>Texture asset, stores in JPEG2000 J2C stream format</summary>
        Texture = 0,
        /// <summary>Sound asset</summary>
        Sound = 1,
        /// <summary>Calling card for another avatar</summary>
        CallingCard = 2,
        /// <summary>Link to a location in world</summary>
        Landmark = 3,
        /// <summary>Legacy script asset, you should never see one of these</summary>
        [Obsolete]
        Script = 4,
        /// <summary>Collection of textures and parameters that can be 
        /// worn by an avatar</summary>
        Clothing = 5,
        /// <summary>Primitive that can contain textures, sounds, 
        /// scripts and more</summary>
        Object = 6,
        /// <summary>Notecard asset</summary>
        Notecard = 7,
        /// <summary>Holds a collection of inventory items</summary>
        Folder = 8,
        /// <summary>Root inventory folder</summary>
        RootFolder = 9,
        /// <summary>Linden scripting language script</summary>
        LSLText = 10,
        /// <summary>LSO bytecode for a script</summary>
        LSLBytecode = 11,
        /// <summary>Uncompressed TGA texture</summary>
        TextureTGA = 12,
        /// <summary>Collection of textures and shape parameters that can
        /// be worn</summary>
        Bodypart = 13,
        /// <summary>Trash folder</summary>
        TrashFolder = 14,
        /// <summary>Snapshot folder</summary>
        SnapshotFolder = 15,
        /// <summary>Lost and found folder</summary>
        LostAndFoundFolder = 16,
        /// <summary>Uncompressed sound</summary>
        SoundWAV = 17,
        /// <summary>Uncompressed TGA non-square image, not to be used as a
        /// texture</summary>
        ImageTGA = 18,
        /// <summary>Compressed JPEG non-square image, not to be used as a
        /// texture</summary>
        ImageJPEG = 19,
        /// <summary>Animation</summary>
        Animation = 20,
        /// <summary>Sequence of animations, sounds, chat, and pauses</summary>
        Gesture = 21,
        /// <summary>Simstate file</summary>
        Simstate = 22,
    }

    /// <summary>
    /// 
    /// </summary>
    public enum StatusCode
    {
        /// <summary>OK</summary>
        OK = 0,
        /// <summary>Transfer completed</summary>
        Done = 1,
        /// <summary></summary>
        Skip = 2,
        /// <summary></summary>
        Abort = 3,
        /// <summary>Unknown error occurred</summary>
        Error = -1,
        /// <summary>Equivalent to a 404 error</summary>
        UnknownSource = -2,
        /// <summary>Client does not have permission for that resource</summary>
        InsufficientPermissiosn = -3,
        /// <summary>Unknown status</summary>
        Unknown = -4
    }

    /// <summary>
    /// 
    /// </summary>
    public enum ChannelType : int
    {
        /// <summary></summary>
        Unknown = 0,
        /// <summary></summary>
        Misc,
        /// <summary></summary>
        Asset
    }

    /// <summary>
    /// 
    /// </summary>
    public enum SourceType : int
    {
        /// <summary></summary>
        Unknown = 0,
        /// <summary>Request arbitrary system files off the server</summary>
        [Obsolete]
        File = 1,
        /// <summary>Request assets from the asset server</summary>
        Asset = 2,
        /// <summary></summary>
        SimInventoryItem = 3,
        /// <summary></summary>
        SimEstate = 4
    }

    /// <summary>
    /// 
    /// </summary>
    public enum TargetType : int
    {
        /// <summary></summary>
        Unknown = 0,
        /// <summary></summary>
        File,
        /// <summary></summary>
        VFile
    }

    /// <summary>
    /// 
    /// </summary>
    public class Transfer
    {
        public LLUUID ID = LLUUID.Zero;
        public int Size = 0;
        public byte[] AssetData = new byte[0];
        public int Transferred = 0;
        public bool Success = false;

        internal ManualResetEvent HeaderReceivedEvent = new ManualResetEvent(false);
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetTransfer : Transfer
    {
        public LLUUID AssetID = LLUUID.Zero;
        public ChannelType Channel = ChannelType.Unknown;
        public SourceType Source = SourceType.Unknown;
        public TargetType Target = TargetType.Unknown;
        public StatusCode Status = StatusCode.Unknown;
        public float Priority = 0.0f;
    }

    /// <summary>
    /// 
    /// </summary>
    public class ImageTransfer : Transfer
    {
        public ushort PacketCount = 0;
        public int Codec = 0;
        public bool NotFound = false;

        internal int InitialDataSize = 0;
    }


    /// <summary>
    /// 
    /// </summary>
    public class AssetManager
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        public delegate void AssetReceivedCallback(AssetTransfer asset);


        /// <summary>
        /// 
        /// </summary>
        public event AssetReceivedCallback OnAssetReceived;


        private SecondLife Client;
        private Dictionary<LLUUID, AssetTransfer> Transfers = new Dictionary<LLUUID, AssetTransfer>();

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="client">A reference to the SecondLife client object</param>
        public AssetManager(SecondLife client)
        {
            Client = client;

            // Transfer Packets for downloading large assets
            Client.Network.RegisterCallback(PacketType.TransferInfo, new NetworkManager.PacketCallback(TransferInfoHandler));
            Client.Network.RegisterCallback(PacketType.TransferPacket, new NetworkManager.PacketCallback(TransferPacketHandler));

            // Xfer packets for uploading large assets
            //Client.Network.RegisterCallback(PacketType.AssetUploadComplete, new NetworkManager.PacketCallback(AssetUploadCompleteHandler));
            //Client.Network.RegisterCallback(PacketType.ConfirmXferPacket, new NetworkManager.PacketCallback(ConfirmXferPacketHandler));
            //Client.Network.RegisterCallback(PacketType.RequestXfer, new NetworkManager.PacketCallback(RequestXferHandler));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="type"></param>
        /// <param name="channel"></param>
        /// <param name="source"></param>
        /// <param name="priority"></param>
        public void RequestAsset(LLUUID assetID, AssetType type, ChannelType channel, SourceType source, float priority)
        {
            // TODO: Should we make this function reusable for changing download priorities?

            AssetTransfer transfer = new AssetTransfer();
            transfer.ID = LLUUID.Random();
            transfer.AssetID = assetID;
            transfer.Priority = priority;
            transfer.Channel = channel;
            transfer.Source = source;

            Console.WriteLine("transfer.ID: " + transfer.ID.ToString());

            // Add this transfer to the dictionary
            lock (Transfers) Transfers[transfer.ID] = transfer;

            // Build the request packet and send it
            TransferRequestPacket request = new TransferRequestPacket();
            request.TransferInfo.ChannelType = (int)channel;
            request.TransferInfo.Priority = priority;
            request.TransferInfo.SourceType = (int)source;
            request.TransferInfo.TransferID = transfer.ID;

            byte[] paramField = new byte[20];
            Array.Copy(assetID.GetBytes(), paramField, 16);
            Array.Copy(Helpers.IntToBytes((int)type), 0, paramField, 16, 4);
            request.TransferInfo.Params = paramField;

            Client.Network.SendPacket(request);
        }

        public void RequestInventoryAsset()
        {
            ;
        }

        public void RequestFileAsset()
        {
            ;
        }

        public void RequestEstateAsset()
        {
            ;
        }

        private void TransferInfoHandler(Packet packet, Simulator simulator)
        {
            TransferInfoPacket info = (TransferInfoPacket)packet;

            if (Transfers.ContainsKey(info.TransferInfo.TransferID))
            {
                AssetTransfer transfer = Transfers[info.TransferInfo.TransferID];
                ChannelType channel = ChannelType.Unknown;
                StatusCode status = StatusCode.Unknown;
                TargetType target = TargetType.Unknown;

                // Attempt to recover enumeration values out of the integers
                channel = (ChannelType)info.TransferInfo.ChannelType;
                status = (StatusCode)info.TransferInfo.Status;
                target = (TargetType)info.TransferInfo.TargetType;

                transfer.Channel = channel;
                transfer.Status = status;
                transfer.Target = target;
                transfer.Size = info.TransferInfo.Size;
                transfer.AssetData = new byte[transfer.Size];
            }
            else
            {
                Client.Log("Received a TransferInfo packet for an asset we didn't request, TransferID: " + 
                    info.TransferInfo.TransferID, Helpers.LogLevel.Warning);

                Console.WriteLine(info.ToString());
            }
        }

        private void TransferPacketHandler(Packet packet, Simulator simulator)
        {
            if (OnAssetReceived != null)
            {
                TransferPacketPacket asset = (TransferPacketPacket)packet;
                Console.WriteLine(asset.ToString());

                if (Transfers.ContainsKey(asset.TransferData.TransferID))
                {
                    AssetTransfer transfer = Transfers[asset.TransferData.TransferID];

                    if (transfer.Size == 0)
                    {
                        // We haven't received the header yet, block until it's received or times out
                        transfer.HeaderReceivedEvent.WaitOne(1000 * 20, false);

                        if (transfer.Size == 0)
                        {
                            Client.Log("Timed out while waiting for the asset header to download for " +
                                transfer.ID.ToStringHyphenated(), Helpers.LogLevel.Warning);

                            lock (Transfers) Transfers.Remove(transfer.ID);

                            // Fire the event with our transfer that contains Success = false;
                            try { OnAssetReceived(transfer); }
                            catch (Exception e) { Client.Log(e.ToString(), Helpers.LogLevel.Error); }

                            return;
                        }
                    }

                    // This assumes that every transfer packet except the last one is exactly 1000 bytes,
                    // hopefully that is a safe assumption to make
                    if (asset.TransferData.Data.Length == 1000 ||
                        transfer.Transferred + asset.TransferData.Data.Length >= transfer.Size)
                    {
                        Array.Copy(asset.TransferData.Data, 0, transfer.AssetData, 1000 * (asset.TransferData.Packet - 1),
                            asset.TransferData.Data.Length);
                        transfer.Transferred += asset.TransferData.Data.Length;
                    }
                    else
                    {
                        Client.Log("Received a TransferPacket with a data length of " + asset.TransferData.Data.Length +
                            " bytes!", Helpers.LogLevel.Error);

                        // fire the even with out transfer that contains Success = false;
                        try { OnAssetReceived(transfer); }
                        catch (Exception e) { Client.Log(e.ToString(), Helpers.LogLevel.Error); }

                        return;
                    }

                    Client.DebugLog("Received " + asset.TransferData.Data.Length + "/" + transfer.Transferred +
                        "/" + transfer.Size + " bytes for asset " + transfer.ID.ToStringHyphenated());

                    // Check if we downloaded the full asset
                    if (transfer.Transferred >= transfer.Size)
                    {
                        transfer.Success = true;
                        lock (Transfers) Transfers.Remove(transfer.ID);

                        try { OnAssetReceived(transfer); }
                        catch (Exception e) { Client.Log(e.ToString(), Helpers.LogLevel.Error); }
                    }
                }
                else
                {
                    Client.Log("Received a TransferPacket packet for an asset we didn't request, TransferID: " +
                        asset.TransferData.TransferID, Helpers.LogLevel.Warning);
                }
            }
        }
    }

    public class ImageManager
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        public delegate void ImageReceivedCallback(ImageTransfer image);

        public event ImageReceivedCallback OnImageReceived;

        private SecondLife Client;
        private Dictionary<LLUUID, ImageTransfer> Transfers = new Dictionary<LLUUID, ImageTransfer>();

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="client">A reference to the SecondLife client to use</param>
        public ImageManager(SecondLife client)
        {
            Client = client;

            Client.Network.RegisterCallback(PacketType.ImageData, new NetworkManager.PacketCallback(ImageDataHandler));
            Client.Network.RegisterCallback(PacketType.ImagePacket, new NetworkManager.PacketCallback(ImagePacketHandler));
            Client.Network.RegisterCallback(PacketType.ImageNotInDatabase, new NetworkManager.PacketCallback(ImageNotInDatabaseHandler));
        }

        /// <summary>
        /// Initiate an image download. This is an asynchronous function
        /// </summary>
        /// <param name="imageID">The image to download</param>
        public void RequestImage(LLUUID imageID, float priority)
        {
            if (!Transfers.ContainsKey(imageID))
            {
                ImageTransfer transfer = new ImageTransfer();
                transfer.ID = imageID;

                // Add this transfer to the dictionary
                lock (Transfers) Transfers[transfer.ID] = transfer;

                // Build and send the request packet
                RequestImagePacket request = new RequestImagePacket();
                request.AgentData.AgentID = Client.Network.AgentID;
                request.AgentData.SessionID = Client.Network.SessionID;
                request.RequestImage = new RequestImagePacket.RequestImageBlock[1];
                request.RequestImage[0] = new RequestImagePacket.RequestImageBlock();
                request.RequestImage[0].DiscardLevel = 0;
                request.RequestImage[0].DownloadPriority = priority;
                request.RequestImage[0].Packet = 0;
                request.RequestImage[0].Image = imageID;
                request.RequestImage[0].Type = 0; // TODO: What is this?

                Client.Network.SendPacket(request);
            }
            else
            {
                Client.Log("RequestImage() called for an image we are already downloading, ignoring",
                    Helpers.LogLevel.Info);
            }
        }

        /// <summary>
        /// Handles the Image Data packet which includes the ID and Size of the image,
        /// along with the first block of data for the image. If the image is small enough
        /// there will be no additional packets
        /// </summary>
        public void ImageDataHandler(Packet packet, Simulator simulator)
        {
            ImageDataPacket data = (ImageDataPacket)packet;

            Client.DebugLog("Received first " + data.ImageData.Data.Length + " bytes for image " +
                data.ImageID.ID.ToStringHyphenated());

            if (Transfers.ContainsKey(data.ImageID.ID))
            {
                ImageTransfer transfer = Transfers[data.ImageID.ID];

                transfer.Codec = data.ImageID.Codec;
                transfer.PacketCount = data.ImageID.Packets;
                transfer.Size = (int)data.ImageID.Size;
                transfer.AssetData = new byte[transfer.Size];
                Array.Copy(data.ImageData.Data, transfer.AssetData, data.ImageData.Data.Length);
                transfer.InitialDataSize = data.ImageData.Data.Length;
                transfer.Transferred += data.ImageData.Data.Length;

                transfer.HeaderReceivedEvent.Set();

                // Check if we downloaded the full image
                if (transfer.Transferred >= transfer.Size)
                {
                    lock (Transfers) Transfers.Remove(transfer.ID);
                    transfer.Success = true;

                    if (OnImageReceived != null)
                    {
                        try { OnImageReceived(transfer); }
                        catch (Exception e) { Client.Log(e.ToString(), Helpers.LogLevel.Error); }
                    }
                }
            }
            else
            {
                Client.Log("Received an ImageData packet for an image we didn't request, ID: " + data.ImageID.ID,
                    Helpers.LogLevel.Warning);
            }
        }

        /// <summary>
        /// Handles the remaining Image data that did not fit in the initial ImageData packet
        /// </summary>
        public void ImagePacketHandler(Packet packet, Simulator simulator)
        {
            ImagePacketPacket image = (ImagePacketPacket)packet;

            if (Transfers.ContainsKey(image.ImageID.ID))
            {
                ImageTransfer transfer = Transfers[image.ImageID.ID];

                if (transfer.Size == 0)
                {
                    // We haven't received the header yet, block until it's received or times out
                    transfer.HeaderReceivedEvent.WaitOne(1000 * 20, false);

                    if (transfer.Size == 0)
                    {

                        Client.Log("Timed out while waiting for the image header to download for " +
                            transfer.ID.ToStringHyphenated(), Helpers.LogLevel.Warning);

                        lock (Transfers) Transfers.Remove(transfer.ID);

                        // Fire the event with our transfer that contains Success = false;
                        if (OnImageReceived != null)
                        {
                            try { OnImageReceived(transfer); }
                            catch (Exception e) { Client.Log(e.ToString(), Helpers.LogLevel.Error); }
                        }

                        return;
                    }
                }

                // The header is downloaded, we can insert this data in to the proper position
                Array.Copy(image.ImageData.Data, 0, transfer.AssetData, transfer.InitialDataSize + (1000 * (image.ImageID.Packet - 1)), image.ImageData.Data.Length);
                transfer.Transferred += image.ImageData.Data.Length;

                Client.DebugLog("Received " + image.ImageData.Data.Length + "/" + transfer.Transferred +
                    "/" + transfer.Size + " bytes for image " + image.ImageID.ID.ToStringHyphenated());

                // Check if we downloaded the full image
                if (transfer.Transferred >= transfer.Size)
                {
                    transfer.Success = true;
                    lock (Transfers) Transfers.Remove(transfer.ID);

                    if (OnImageReceived != null)
                    {
                        try { OnImageReceived(transfer); }
                        catch (Exception e) { Client.Log(e.ToString(), Helpers.LogLevel.Error); }
                    }
                }
            }
            else
            {
                Client.Log("Received an ImagePacket packet for an image we didn't request, ID: " + image.ImageID.ID,
                    Helpers.LogLevel.Warning);
            }
        }

        /// <summary>
        /// The requested image does not exist on the asset server
        /// </summary>
        public void ImageNotInDatabaseHandler(Packet packet, Simulator simulator)
        {
            ImageNotInDatabasePacket notin = (ImageNotInDatabasePacket)packet;

            if (Transfers.ContainsKey(notin.ImageID.ID))
            {
                ImageTransfer transfer = Transfers[notin.ImageID.ID];

                transfer.NotFound = true;
                lock (Transfers) Transfers.Remove(transfer.ID);

                // Fire the event with our transfer that contains Success = false;
                if (OnImageReceived != null)
                {
                    OnImageReceived(transfer);
                }
            }
            else
            {
                Client.Log("Received an ImageNotInDatabase packet for an image we didn't request, ID: " +
                    notin.ImageID.ID, Helpers.LogLevel.Warning);
            }
        }
    }
}
