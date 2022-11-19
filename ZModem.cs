using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

class ZModem
{

    /* 
     * 
     * sources from:
     * http://gallium.inria.fr/~doligez/zmodem/zmodem.txt
     * https://stackoverflow.com/questions/9611000/understanding-the-zmodem-protocol
     * https://sources.debian.org/src/lrzsz/0.12.21-10/src/lsz.c/
     * http://wiki.synchro.net/ref:zmodem
     * https://crccalc.com/
     */

    //All frames sent by the “receiver” must use HEX encoding. 
    //

    #region Constants

    /// <summary>
    /// Header types
    /// </summary>
    public enum HeaderType
    {
        /// <summary>
        /// Request receive init
        /// </summary>
        ZRQINIT = 0,
        /// <summary>
        /// Receive init
        /// </summary>
        ZRINIT = 1,
        /// <summary>
        /// Send init sequence (optional)
        /// </summary>
        ZSINIT = 2,
        /// <summary>
        /// ACK to above
        /// </summary>
        ZACK = 3,
        /// <summary>
        /// File name from sender
        /// </summary>
        ZFILE = 4,
        /// <summary>
        /// To sender: skip this file
        /// </summary>
        ZSKIP = 5,
        /// <summary>
        /// Last packet was garbled
        /// </summary>
        ZNAK = 6,
        /// <summary>
        /// Abort batch transfers
        /// </summary>
        ZABORT = 7,
        /// <summary>
        /// Finish session
        /// </summary>
        ZFIN = 8,
        /// <summary>
        /// Resume data trans at this position
        /// </summary>
        ZRPOS = 9,
        /// <summary>
        /// Data packet(s) follow
        /// </summary>
        ZDATA = 10,
        /// <summary>
        /// End of file
        /// </summary>
        ZEOF = 11,
        /// <summary>
        /// Fatal Read or Write error Detected
        /// </summary>
        ZFERR = 12,
        /// <summary>
        /// Request for file CRC and response
        /// </summary>
        ZCRC = 13,
        /// <summary>
        /// Receiver's Challenge
        /// </summary>
        ZCHALLENGE = 14,
        /// <summary>
        /// Request is complete
        /// </summary>
        ZCOMPL = 15,
        /// <summary>
        /// Other end canned session with CAN*5
        /// </summary>
        ZCAN = 16,
        /// <summary>
        /// Request for free bytes on filesystem
        /// </summary>
        ZFREECNT = 17,
        /// <summary>
        /// Command from sending program
        /// </summary>
        ZCOMMAND = 18,
        /// <summary>
        /// Output to standard error, data follows
        /// </summary>
        ZESTERR = 19,
        None = -1,
    };

    /// <summary>
    /// Control bytes
    /// </summary>
    public enum ControlBytes
    {
        /// <summary>
        /// 0x2A Padding character begins frames
        /// </summary>
        ZPAD = '*',
        /// <summary>
        /// Ctrl-cc Zmodem escape - `ala BISYNC DLE
        /// </summary>
        ZDLE = 0x18,
        /// <summary>
        /// Escaped ZDLE as transmitted
        /// </summary>
        ZDLEE = 0x58,
        /// <summary>
        /// Binary frame indicator
        /// </summary>
        ZBIN = 'A',
        /// <summary>
        /// HEX frame indicator
        /// </summary>
        ZHEX = 'B',
        /// <summary>
        /// Binary frame with 32 bit FCS
        /// </summary>
        ZBINC = 'C',
        XON = 0x11,
        XOFF = 0x13,
        /// <summary>
        /// CR character
        /// </summary>
        CR = 0x0d,
        /// <summary>
        /// LF character
        /// </summary>
        LF = 0x0a,
        DLE = 0x10,
        CTRL0x90 = 0x90,
        RI = 0x8d,
        ATSymbol = 0x40,
        À = 0xc0
    }

    /// <summary>
    /// Parameters for ZSINIT frame
    /// </summary>
    public enum ZSINIT
    {
        /// <summary>
        /// Transmitter expects ctl chars to be escaped
        /// </summary>
        TESCCTL = 64,
        /// <summary>
        /// Transmitter expects 8th bit to be escaped
        /// </summary>
        TESC8 = 128
    }

    /// <summary>
    /// Conversion options for ZFILE header
    /// </summary>
    public enum ZFILEConversionOption
    {
        /// <summary>
        /// None
        /// </summary>
        None = 0,
        /// <summary>
        /// Binary transfer - inhibit conversion
        /// </summary>
        ZCBIN = 1,
        /// <summary>
        /// Convert NL to local end of line convention
        /// </summary>
        ZCNL = 2,
        /// <summary>
        /// Resume interrupted file transfer
        /// </summary>
        ZCRESUM = 3
    }

    /// <summary>
    /// Management options for ZFILE header
    /// </summary>
    public enum ZFILEManagementOption
    {
        /// <summary>
        /// None
        /// </summary>
        None = 0,
        /// <summary>
        /// Skip file if not present at rx
        /// </summary>
        ZMSKNOLOC = 128,
        /// <summary>
        /// Mask for the choices below
        /// </summary>
        ZMMASK = 31,
        /// <summary>
        /// Transfer if source newer or longer
        /// </summary>
        ZMNEWL = 1,
        /// <summary>
        /// Transfer if different file CRC or length
        /// </summary>
        ZMCRC = 2,
        /// <summary>
        /// Append contents to existing file (if any)
        /// </summary>
        ZMAPND = 3,
        /// <summary>
        /// Replace existing file
        /// </summary>
        ZMCLOB = 4,
        /// <summary>
        /// Transfer if source newer
        /// </summary>
        ZMNEW = 5,
        /// <summary>
        /// Transfer if dates or lengths different
        /// </summary>
        ZMDIFF = 6,
        /// <summary>
        /// Protect destination file
        /// </summary>
        ZMPROT = 7
    }

    /// <summary>
    /// Transport options for ZFile header
    /// </summary>
    public enum ZFILETransportOption
    {
        /// <summary>
        /// None
        /// </summary>
        None = 0,
        /// <summary>
        /// Lempel-Ziv compression
        /// </summary>
        ZTLZW = 1,
        /// <summary>
        /// Encryption
        /// </summary>
        ZTCRYPT = 2,
        /// <summary>
        /// Run Length encoding
        /// </summary>
        ZTRLE = 3
    }

    /// <summary>
    /// Extended options for ZFILE header
    /// </summary>
    public enum ZFILEExtendedOptions
    {
        /// <summary>
        /// None
        /// </summary>
        None = 0,
        /// <summary>
        /// Encoding for sparse file operations
        /// </summary>
        ZXSPARS = 64
    }

    /// <summary>
    /// ZDLE Sequences
    /// </summary>
    public enum ZDLESequence
    {
        /// <summary>
        /// CRC next, frame ends, header packet follows
        /// </summary>
        ZCRCE = 'h',
        /// <summary>
        /// CRC next, frame continues nonstop
        /// </summary>
        ZCRCG = 'i',
        /// <summary>
        /// CRC next, frame continues, ZACK expected
        /// </summary>
        ZCRCQ = 'j',
        /// <summary>
        /// CRC next, ZACK expected, end of frame
        /// </summary>
        ZCRCW = 'k',
        /// <summary>
        /// Translate to rubout 0177
        /// </summary>
        ZRUB0 = 'l',
        /// <summary>
        /// Translate to rubout 0377
        /// </summary>
        ZRUB1 = 'm',
    }

    /// <summary>
    /// Parameters for ZCOMMAND frame ZF0 (otherwise 0)
    /// </summary>
    public enum ZSCommandHeader
    {
        /// <summary>
        /// None
        /// </summary>
        None = 0,
        /// <summary>
        /// Acknowledge, then do command
        /// </summary>
        ZCACK1 = 1
    }

    /// <summary>
    /// Bit Masks for ZRINIT flags byte ZF0
    /// </summary>
    public enum ZRINITFlags
    {
        /// <summary>
        /// Rx can send and receive true FDX
        /// </summary>
        CANFDX = 0x01,
        /// <summary>
        /// Rx can receive data during disk I/O
        /// </summary>
        CANOVIO = 0x02,
        /// <summary>
        /// Rx can send a break signal
        /// </summary>
        CANBRK = 0x04,
        /// <summary>
        /// Receiver can decode RLE
        /// </summary>
        CANRLE = 0x08,
        /// <summary>
        /// Receiver can uncompress
        /// </summary>
        CANLZW = 0x10,
        /// <summary>
        /// Receiver can use 32 bit Frame Check
        /// </summary>
        CANFC32 = 0x20,
        /// <summary>
        /// Receiver expects ctl chars to be escaped
        /// </summary>
        ESCCTL = 0x40,
        /// <summary>
        /// Receiver expects 8th bit to be escaped
        /// </summary>
        ESC8 = 0x80,
        /// <summary>
        /// 9th byte in header contains window size/256,
        /// </summary>
        ZRPXWN = 8,
        /// <summary>
        /// 10th to 14th bytes contain quote mask
        /// </summary>
        ZRPXQQ = 9
    }
    #endregion

    #region CRC
    public static class CRC16
    {
        const ushort polynomial = 0x1021;
        static readonly ushort[] table = new ushort[256];

        public static ushort ComputeChecksum(IEnumerable<byte> bytes)
        {
            ushort crc = 0;
            foreach (var b in bytes)
            {
                var t = (byte)((crc >> 8) ^ b);
                crc = (ushort)((crc << 8) ^ table[t]);
            }
            return crc;
        }

        public static ushort ContinueComputeChecksum(ushort crc, IEnumerable<byte> bytes)
        {
            foreach (var b in bytes)
            {
                var t = (byte)((crc >> 8) ^ b);
                crc = (ushort)((crc << 8) ^ table[t]);
            }
            return crc;
        }

        static CRC16()
        {
            for (ushort i = 0; i < table.Length; i++)
            {
                ushort crc = (ushort)(i << 8);
                for (byte bit = 0; bit < 8; bit++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ polynomial);
                    else
                        crc <<= 1;
                }
                table[i] = crc;
            }
        }

        public static bool Match(byte[] buf, ushort crc)
        {
            if (buf[0] != (crc >> 8)) return false;
            if (buf[1] != (crc & 0xff)) return false;
            return true;
        }

        public static IEnumerable<byte> GetBytes(ushort crc)
        {
            yield return (byte)(crc >> 8);
            yield return (byte)(crc & 0xff);
        }
    }

    public static class CRC32
    {
        const uint polynomial = 0x04C11DB7;
        static readonly uint[] table = new uint[256];

        public static uint ComputeChecksum(IEnumerable<byte> bytes)
        {
            uint crc = 0xffffffff;
            foreach (var b in bytes)
            {
                var t = (byte)((crc ^ (reverse8(b) << 24)) >> 24);
                crc = ((crc << 8) ^ table[t]);
            }
            return crc;
        }

        public static uint ContinueComputeChecksum(uint crc, IEnumerable<byte> bytes)
        {
            foreach (var b in bytes)
            {
                var t = (byte)((crc ^ (reverse8(b) << 24)) >> 24);
                crc = ((crc << 8) ^ table[t]);
            }
            return crc;
        }

        public static uint RevCRC(uint crc) => reverse32(crc) ^ 0xffffffff;

        static uint reverse32(uint value)
        {
            value = ((value & 0xAAAAAAAA) >> 1) | ((value & 0x55555555) << 1);
            value = ((value & 0xCCCCCCCC) >> 2) | ((value & 0x33333333) << 2);
            value = ((value & 0xF0F0F0F0) >> 4) | ((value & 0x0F0F0F0F) << 4);
            value = ((value & 0xFF00FF00) >> 8) | ((value & 0x00FF00FF) << 8);
            value = (value >> 16) | (value << 16);
            return value;
        }

        static byte reverse8(byte value)
        {
            value = (byte)((value & 0xF0) >> 4 | (value & 0x0F) << 4);
            value = (byte)((value & 0xCC) >> 2 | (value & 0x33) << 2);
            value = (byte)((value & 0xAA) >> 1 | (value & 0x55) << 1);
            return value;
        }

        static CRC32()
        {
            var revpoly = reverse32(polynomial);
            for (uint i = 0; i < table.Length; i++)
            {
                uint crc = reverse8((byte)i);
                for (byte bit = 0; bit < 8; bit++)
                {
                    if ((crc & 0x01) != 0)
                        crc = (crc >> 1) ^ revpoly;
                    else
                        crc >>= 1;
                }
                table[i] = reverse32(crc);
            }
        }

        public static bool Match(byte[] buf, uint crc)
        {
            if (buf[0] != (crc & 0xff)) return false;
            if (buf[1] != ((crc >> 8) & 0xFF)) return false;
            if (buf[2] != ((crc >> 16) & 0xFF)) return false;
            if (buf[3] != (crc >> 24)) return false;
            return true;
        }

        public static IEnumerable<byte> GetBytes(uint crc)
        {
            yield return (byte)(crc & 0xff);
            yield return (byte)((crc >> 8) & 0xff);
            yield return (byte)((crc >> 16) & 0xff);
            yield return (byte)(crc >> 24);
        }
    }
    #endregion


#pragma warning disable CS8618

    bool allow32bit = false;

    public event Action<byte[]> OnData;
    public event Action<uint> OnProgress;
    public event Action<string> OnError;
    public event Action OnCompleteFile;
    public event Action OnRecieveRequest;
    public event Action OnSendRequest;
    public event Action<ZFileInfo> OnAcceptFile;
    public event Action OnFinish;

    public class ZFileInfo
    {
        public string fname;
        public string fullname;
        public long fsize;
        public DateTime fdate;
    }

    readonly List<ZFileInfo> files_to_send = new List<ZFileInfo>();

    enum EMode
    {
        None,
        Sending,
        SendingFin,
        Receiving
    }

    EMode mode = EMode.None;
    uint send_pos, recv_pos;

    FileStream recv_file;
    FileStream? send_file = null;

    int state = 0;
    bool wide = false;
    List<byte> hdrframe = new List<byte>();
    List<byte> pckframe = new List<byte>();

    int crc_cnt = 0;
    bool escape_byte = false;
    int recv_fail_count = 0;

    byte[] sendbuf = new byte[2048];

    public void SetFiles(string[] fnames)
    {
        files_to_send.Clear();
        foreach (var fname in fnames)
        {
            var fi = new FileInfo(fname);
            files_to_send.Add(new ZFileInfo()
            {
                fname = Path.GetFileName(fname),
                fullname = fname,
                fsize = fi.Length,
                fdate = File.GetLastWriteTimeUtc(fname)
            });
        }
    }

    public void StartReceiving()
    {
        if (mode != EMode.None) return;
        mode = EMode.Receiving;
        sendZRINIT();
    }

    public void AcceptFileAs(string fname)
    {
        if (mode != EMode.Receiving) return;

        recv_file = File.OpenWrite(fname);
        recv_pos = 0;

        sendZRPOS(recv_pos);
    }

    public void SkipFile()
    {
        if (mode != EMode.Receiving) return;
        sendHEXHeader(buildHeaderFrame((byte)HeaderType.ZSKIP, 0x00, 0x00, 0x00, 0x00));
    }

    public void DenySending()
    {
        if (mode != EMode.None) return;
        mode = EMode.Sending;
        sendHEXHeader(buildHeaderFrame((byte)HeaderType.ZFIN, 0x00, 0x00, 0x00, 0x00));
    }

    public void StartSending()
    {
        if (mode != EMode.None) return;
        if (files_to_send.Count == 0) return;

        mode = EMode.Sending;
        nextSend();
    }

    public bool ShowByte => state < 30;
    public void RecvByte(byte b)
    {
        byte fin_packet = 0;
        if (state >= 30 && state < 40)
        {
            if (escape_byte)
            {
                if ((b & 0xF8) == 0x68)
                    fin_packet = b;

                b = (byte)(b ^ 0x40);
                escape_byte = false;
            }
            else if (b == (byte)ControlBytes.ZDLE)
            {
                escape_byte = true;
                return;
            }
        }
        else escape_byte = false;

        switch (state)
        {
        case 0:
            if (b == (byte)ControlBytes.ZPAD) state = 1;
            break;
        case 1: //ZPAD
            if (b == (byte)ControlBytes.ZPAD) state = 2;
            else if (b == (byte)ControlBytes.ZDLE) state = 4;
            else state = 0;
            break;
        case 2: //ZPAD ZPAD
            if (b == (byte)ControlBytes.ZDLE) state = 3;
            else state = 0;
            break;
        case 3: //ZPAD ZPAD ZDLE
            if (b == (byte)ControlBytes.ZHEX) { state = 20; wide = false; hdrframe.Clear(); }
            else state = 0;
            break;
        case 4: //ZPAD ZDLE
            if (b == (byte)ControlBytes.ZBIN) { state = 30; wide = false; hdrframe.Clear(); }
            else if (b == (byte)ControlBytes.ZBINC) { state = 30; wide = true; hdrframe.Clear(); }
            else state = 0;
            break;
        case 20: //HEX PACKET HI
            if (b == 0x0D) state = procHeader();
            else if (b >= 0x30 && b <= 0x39) { hdrframe.Add((byte)((b - 0x30) << 4)); state = 21; }
            else if (b >= 0x61 && b <= 0x66) { hdrframe.Add((byte)((b - 0x57) << 4)); state = 21; }
            else state = 0;
            break;
        case 21: //HEX PACKET LO
            if (b >= 0x30 && b <= 0x39) { hdrframe[hdrframe.Count - 1] |= (byte)(b - 0x30); state = 20; }
            else if (b >= 0x61 && b <= 0x66) { hdrframe[hdrframe.Count - 1] |= (byte)(b - 0x57); state = 20; }
            else state = 0;
            break;
        case 30: //BIN PACKET
            hdrframe.Add(b);
            if (!wide && hdrframe.Count == 7) state = procHeader();
            if (hdrframe.Count == 9) state = procHeader();
            break;
        case 31: //subpacket
            if (fin_packet != 0)
            {
                pckframe.Add(fin_packet);
                crc_cnt = 0;
                state = 32;
                break;
            }
            pckframe.Add(b);
            break;
        case 32: //expect CRC
            pckframe.Add(b);
            crc_cnt++;
            if ((crc_cnt == 4) || (!wide && crc_cnt == 2))
                state = procPacket();
            break;

        //OO over-and-out
        case 40:
            if (b == 0x4f) state = 41;
            else if (b == (byte)ControlBytes.ZPAD) state = 1;
            break;
        case 41:
            if (b == 0x4f)
            {
                Trace.WriteLine($"RECV OO");
                state = 0;
            }
            else if (b == (byte)ControlBytes.ZPAD) state = 1;
            else state = 0;
            break;
        }
    }


    void nextSend()
    {
        if (files_to_send.Count == 0)
            sendBINHeader(buildHeaderFrame((byte)HeaderType.ZFIN, 0, 0, 0, 0));
        else
            sendZFILE();
    }

    static IEnumerable<byte> escape(IEnumerable<byte> data)
    {
        foreach (var b in data)
        {
            switch (b)
            {
            case (byte)ControlBytes.ZDLE:
            case 0x10:
            case 0x90:
            case 0x11:
            case 0x91:
            case 0x13:
            case 0x93:
                yield return (byte)ControlBytes.ZDLE;
                yield return (byte)(b ^ 0x40);
                break;
            default:
                yield return b;
                break;
            }
        }
    }

    void sendHEXHeader(byte[] data)
    {
        var frame = new List<byte>(12 + 2 * data.Length);
        frame.Add((byte)ControlBytes.ZPAD);
        frame.Add((byte)ControlBytes.ZPAD);
        frame.Add((byte)ControlBytes.ZDLE);
        frame.Add((byte)ControlBytes.ZHEX);

        void addhexbyte(List<byte> frame, byte b)
        {
            var s = b.ToString("x2");
            frame.Add((byte)s[0]);
            frame.Add((byte)s[1]);
        }

        foreach (var b in data)
            addhexbyte(frame, b);

        var crc = CRC16.ComputeChecksum(data);
        addhexbyte(frame, (byte)(crc >> 8));
        addhexbyte(frame, (byte)(crc & 0xFF));

        frame.Add((byte)ControlBytes.CR);
        frame.Add((byte)ControlBytes.LF);
        frame.Add((byte)ControlBytes.XON);

        OnData?.Invoke(frame.ToArray());
    }

    void sendBINHeader(IEnumerable<byte> data)
    {
        var buf = escape(data);
        if (allow32bit) buf = buf.Prepend((byte)ControlBytes.ZBINC);
        else buf = buf.Prepend((byte)ControlBytes.ZBIN);

        buf = buf.Prepend((byte)ControlBytes.ZDLE);
        buf = buf.Prepend((byte)ControlBytes.ZPAD);

        if (allow32bit)
        {
            var crc = CRC32.RevCRC(CRC32.ComputeChecksum(data));
            buf = buf.Concat(escape(CRC32.GetBytes(crc)));
        }
        else
        {
            var crc = CRC16.ComputeChecksum(data);
            buf = buf.Concat(escape(CRC16.GetBytes(crc)));
        }

        Trace.WriteLine("H=> " + buf.Aggregate("", (a, b) => a + b.ToString("x2")));
        OnData?.Invoke(buf.ToArray());
    }

    void sendBINFrame(IEnumerable<byte> frame, byte frame_end_type)
    {
        var spt = new byte[1] { frame_end_type };
        var buf = escape(frame)
            .Append((byte)ControlBytes.ZDLE)
            .Concat(escape(spt));

        if (allow32bit)
        {
            var crc_sp = CRC32.ComputeChecksum(frame);
            crc_sp = CRC32.ContinueComputeChecksum(crc_sp, spt);
            crc_sp = CRC32.RevCRC(crc_sp);
            buf = buf.Concat(escape(CRC32.GetBytes(crc_sp)));
        }
        else
        {
            var crc_sp = CRC16.ComputeChecksum(frame);
            crc_sp = CRC16.ContinueComputeChecksum(crc_sp, spt);
            buf = buf.Concat(escape(CRC16.GetBytes(crc_sp)));
        }

        OnData?.Invoke(buf.ToArray());
    }

    bool checkCRC(IEnumerable<byte> frame)
    {
        if (wide)
        {
            var crc = CRC32.RevCRC(CRC32.ComputeChecksum(frame.SkipLast(4)));
            if (!CRC32.Match(frame.TakeLast(4).ToArray(), crc))
            {
                Trace.WriteLine("CRC32 Error");
                return false;
            }
        }
        else
        {
            var crc = CRC16.ComputeChecksum(frame.SkipLast(2));
            if (!CRC16.Match(frame.TakeLast(2).ToArray(), crc))
            {
                Trace.WriteLine("CRC16 Error");
                return false;
            }
        }

        //WriteToLog.Invoke("CRC OK");
        return true;
    }

    int procHeader()
    {
        if (!checkCRC(hdrframe)) 
            return 0;
        uint rpos;

        switch (hdrframe[0])
        {
        case (byte)HeaderType.ZRQINIT:
            Trace.WriteLine("RECV ZRQINIT");
            OnRecieveRequest?.Invoke();
            break;
        case (byte)HeaderType.ZRINIT:
            int flags = BitConverter.ToInt32(hdrframe.Skip(1).Take(4).Reverse().ToArray());
            allow32bit = (flags & (int)ZRINITFlags.CANFC32) != 0;
            Trace.WriteLine($"RECV ZRINIT={hdrframe.Skip(1).Take(4).Aggregate("", (a, b) => a + b.ToString("x2"))}");
            if (mode == EMode.None)
                OnSendRequest?.Invoke();
            else if (mode == EMode.Sending)
                nextSend();
            else if (mode == EMode.SendingFin)
            {
                send_file?.Close();
                send_file = null;
                files_to_send.RemoveAt(0);
                mode = EMode.Sending;
                nextSend();
            }
            break;

        case (byte)HeaderType.ZFILE:
            Trace.WriteLine($"RECV ZFILE");
            pckframe.Clear();
            return 31;
        case (byte)HeaderType.ZDATA:
            rpos = BitConverter.ToUInt32(hdrframe.Skip(1).Take(4).ToArray());
            Trace.WriteLine($"RECV ZDATA={rpos}");
            pckframe.Clear();
            if (rpos != recv_pos)
            {
                recv_fail_count++;
                if (recv_fail_count > 5) { state = 0; mode = EMode.None; OnError?.Invoke("Fail count exceeded"); }
                else sendZRPOS(recv_pos);
            }
            return 31;
        case (byte)HeaderType.ZRPOS:
            if (mode != EMode.Sending) break;

            send_pos = BitConverter.ToUInt32(hdrframe.Skip(1).Take(4).ToArray());
            Trace.WriteLine($"RECV ZRPOS={send_pos}");

            sendZDATA();
            break;
        case (byte)HeaderType.ZEOF:
            rpos = BitConverter.ToUInt32(hdrframe.Skip(1).Take(4).ToArray());
            Trace.WriteLine($"RECV ZEOF={rpos}");
            if (mode != EMode.Receiving) break;
            if (rpos != recv_pos)
            {
                recv_fail_count++;
                if (recv_fail_count > 5) { state = 0; mode = EMode.None; OnError?.Invoke("Fail count exceeded"); }
                else sendZRPOS(recv_pos);
            }
            else
            {
                recv_file.Close();
                OnCompleteFile?.Invoke();
                sendZRINIT();
            }
            break;
        case (byte)HeaderType.ZFIN:
            Trace.WriteLine($"RECV ZFIN");
            if (mode == EMode.Sending)
            {
                OnData?.Invoke(new byte[2] { 0x4f, 0x4f });
                Trace.WriteLine($"SEND OO");
                mode = EMode.None;
            }
            else if (mode == EMode.Receiving)
            {
                sendHEXHeader(buildHeaderFrame((byte)HeaderType.ZFIN, 0, 0, 0, 0));
                Trace.WriteLine($"SEND ZFIN");
                mode = EMode.None;
                OnFinish?.Invoke();
            }
            return 0; //return 40;
        case (byte)HeaderType.ZSKIP:
            Trace.WriteLine($"RECV SKIP");
            break;
        case (byte)HeaderType.ZACK:
            send_pos = BitConverter.ToUInt32(hdrframe.Skip(1).Take(4).ToArray());
            Trace.WriteLine($"RECV ZACK={send_pos}");
            if (mode == EMode.Sending)
                sendZDATA();
            break;
        case (byte)HeaderType.ZNAK:
            Trace.WriteLine($"RECV ZNACK {hdrframe.Skip(1).Take(4).Aggregate("", (a, b) => a + b.ToString("x2"))}");
            break;
        }

        return 0;
    }
    int procPacket()
    {
        if (mode == EMode.None) return 0;

        if (!checkCRC(pckframe))
        {
            if (hdrframe[0] == (byte)HeaderType.ZDATA)
            {
                recv_fail_count++;
                if (recv_fail_count > 5)
                {
                    state = 0; mode = EMode.None;
                    OnError?.Invoke("Fail count exceeded");
                }
                else sendZRPOS(recv_pos);
            }

            return 0;
        }

        int ft_len = wide ? 5 : 3;

        switch (hdrframe[0])
        {
        case (byte)HeaderType.ZFILE:

            var sbfname = new StringBuilder();
            var sbopt = new StringBuilder();
            bool on_fname = true;
            foreach (var b in pckframe)
            {
                if (on_fname)
                {
                    if (b == 0) on_fname = false;
                    else sbfname.Append((char)b);
                }
                else
                {
                    if (b == 0) break;
                    sbopt.Append((char)b);
                }
            }
            var opts = sbopt.ToString().Split(new char[1] { ' ' });
            Trace.WriteLine($"RECV P-ZFILE: {sbfname} size: {opts[0]}");

            var zi = new ZFileInfo()
            {
                fname = sbfname.ToString(),
                fsize = int.Parse(opts[0]),
                fdate = DateTime.UnixEpoch.AddSeconds(Convert.ToInt32(opts[1], 8))
            };

            recv_fail_count = 0;
            OnAcceptFile?.Invoke(zi);
            return 0;

        case (byte)HeaderType.ZDATA:
            Trace.WriteLine($"RECV P-ZDATA: {pckframe.Count - (wide ? 5 : 3)}bytes");
            if (mode == EMode.Receiving)
            {
                recv_file.Position = recv_pos;
                recv_file.Write(pckframe.SkipLast(ft_len).ToArray(), 0, pckframe.Count - ft_len);
                recv_pos += (uint)(pckframe.Count - ft_len);
                OnProgress?.Invoke(recv_pos);
                recv_fail_count = 0;
            }
            else return 0;
            break;

        default:
            return 0;
        }


        byte seq = pckframe.TakeLast(ft_len).First();

        pckframe.Clear();

        switch (seq)
        {
        case (byte)ZDLESequence.ZCRCE: return 0;
        case (byte)ZDLESequence.ZCRCG: return 31;
        case (byte)ZDLESequence.ZCRCQ: sendACK(recv_pos); return 31;
        case (byte)ZDLESequence.ZCRCW: sendACK(recv_pos); return 0;
        }

        return 0;
    }    

    byte[] buildHeaderFrame(byte hdr, byte zp0, byte zp1, byte zp2, byte zp3)
        => new byte[] { hdr, zp0, zp1, zp2, zp3 };

    IEnumerable<byte> buildNextFileSubpacket()
    {
        var f = files_to_send.First();

        //fname
        foreach (var b in Encoding.ASCII.GetBytes(f.fname.ToLower()))
            yield return b;
        yield return 0;
        //fsize
        foreach (var b in f.fsize.ToString())
            yield return (byte)b;
        yield return 0x20;
        //date
        var ms = (uint)f.fdate.Subtract(DateTime.UnixEpoch).TotalSeconds;
        foreach (var b in Convert.ToString(ms, 8))
            yield return (byte)b;
        yield return 0x20;
        //mode
        foreach (var b in "100644")
            yield return (byte)b;
        yield return 0x20;
        //serial number ? = 0
        yield return (byte)'0';
        yield return 0x20;
        //files remaining
        foreach (var b in files_to_send.Count.ToString())
            yield return (byte)b;
        yield return 0x20;
        //bytes remaining
        foreach (var b in files_to_send.Sum(k => k.fsize).ToString())
            yield return (byte)b;
        yield return 0;
    }

    public void sendZRINIT() //sent by receiver for handshake
    {
        byte flags = 0;
        flags |= (byte)ZRINITFlags.CANFC32; //can accept CRC32
        flags |= (byte)ZRINITFlags.CANOVIO; //can receive data during disk I/O
        //flags |= (byte)ZRINITFlags.CANFDX; //can full duplex (to test)
        //ZP0 and ZP1 contain the size of the receiver's buffer in bytes, or 0 if nonstop I/O is allowed.
        sendHEXHeader(buildHeaderFrame((byte)HeaderType.ZRINIT, 0x00, 0x00, 0x00, flags));
    }

    public void sendZFILE()
    {
        send_file = null;

        byte convers_opt = 0;
        byte manag_opt = (byte)ZFILEManagementOption.ZMDIFF;
        byte transport_opt = 0;
        byte extended_opt = 0;

        sendBINHeader(buildHeaderFrame((byte)HeaderType.ZFILE, extended_opt, transport_opt, manag_opt, convers_opt));
        sendBINFrame(buildNextFileSubpacket(), (byte)ZDLESequence.ZCRCW);
    }

    public void sendZRPOS(uint pos)
    {
        var posbytes = BitConverter.GetBytes(pos);
        sendHEXHeader(buildHeaderFrame((byte)HeaderType.ZRPOS, posbytes[0], posbytes[1], posbytes[2], posbytes[3]));
    }

    public void sendACK(uint pos)
    {
        var posbytes = BitConverter.GetBytes(pos);
        sendHEXHeader(buildHeaderFrame((byte)HeaderType.ZACK, posbytes[0], posbytes[1], posbytes[2], posbytes[3]));
    }    

    public void sendZDATA()
    {
        if (send_file == null)
            send_file = File.OpenRead(files_to_send.First().fullname);

        var posbytes = BitConverter.GetBytes(send_pos);

        if (send_pos == send_file.Length) //send_pos is outside of file
        {
            sendBINHeader(buildHeaderFrame((byte)HeaderType.ZEOF, posbytes[0], posbytes[1], posbytes[2], posbytes[3]));
            //send_file.Close();
            //send_file = null;
            //files_to_send.RemoveAt(0);
            return;
        }


        send_file.Position = send_pos;
        int cnt = send_file.Read(sendbuf, 0, sendbuf.Length);

        if (cnt + send_pos >= send_file.Length)
        {
            sendBINHeader(buildHeaderFrame((byte)HeaderType.ZDATA, posbytes[0], posbytes[1], posbytes[2], posbytes[3]));
            sendBINFrame(sendbuf.Take(cnt), (byte)ZDLESequence.ZCRCE);

            send_pos += (uint)cnt;
            OnProgress?.Invoke(send_pos);
            
            mode = EMode.SendingFin;
            posbytes = BitConverter.GetBytes(send_pos);
            sendBINHeader(buildHeaderFrame((byte)HeaderType.ZEOF, posbytes[0], posbytes[1], posbytes[2], posbytes[3]));
            Trace.WriteLine($"SENT ZEOF {send_pos}");

            OnCompleteFile?.Invoke();
        }
        else
        {
            sendBINHeader(buildHeaderFrame((byte)HeaderType.ZDATA, posbytes[0], posbytes[1], posbytes[2], posbytes[3]));
            sendBINFrame(sendbuf.Take(cnt), (byte)ZDLESequence.ZCRCW);
            OnProgress?.Invoke(send_pos);
        }
    }
}

