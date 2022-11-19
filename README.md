Compact ZMODEM for C#
---------------------
Free license


public methods and events
-------------------------

//communication with the module

public event Action<byte[]> OnData; 
public void RecvByte(byte b)


//file transfer

public event Action<uint> OnProgress;
public event Action<string> OnError;
public event Action OnCompleteFile;
public event Action OnRecieveRequest;
public event Action OnSendRequest;
public event Action<ZFileInfo> OnAcceptFile;
public event Action OnFinish;

public void SetFiles(string[] fnames)
public void StartReceiving()
public void AcceptFileAs(string fname)
public void SkipFile()
public void DenySending()
public void StartSending()

    
