using System.IO.Ports;
using System.Text;
using Exchoribur.Application.Abstractions;

namespace Exchoribur.Infrastructure.Devices;

/// <summary>
/// 串口通道:一次写一整帧,写完把缓冲推出去,确保真的出线了。
/// </summary>
/// <remarks>
/// 同时**必须收**:发射器把日志从同一个串口吐回来(每帧一行)。没人读的话,设备侧的发送缓冲
/// 迟早写满,固件里的 Serial.printf 就会一直阻塞 —— 表现是"连续发一阵子之后卡住、空口断几秒",
/// 而且日志本身也看不到。收下来的日志带本地时间戳写成文件,方便和 SDR 录音对齐。
/// </remarks>
internal sealed class SerialChannel : IDeviceChannel
{
    private readonly SerialPort _port;
    private readonly string? _logPath;
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _reader;

    public SerialChannel(SerialPort port, string? logPath)
    {
        _port = port;
        _logPath = logPath;
        _reader = new Thread(PumpDeviceLog) { IsBackground = true, Name = "device-serial-log" };
        _reader.Start();
    }

    public void Send(byte[] frame)
    {
        _port.Write(frame, 0, frame.Length);
        _port.BaseStream.Flush();
    }

    public void Dispose()
    {
        _stop.Cancel();

        // 读线程正卡在 Read 上(最多 ReadTimeout),别把关闭流程拖住。
        _reader.Join(TimeSpan.FromSeconds(1));
        _port.Dispose();
        _stop.Dispose();
    }

    /// <summary>把设备吐回来的日志收下来,按行加本地时间戳写文件。</summary>
    private void PumpDeviceLog()
    {
        if (_logPath is null)
        {
            // 没给日志路径也得照样读:不然设备照样会被自己的日志堵住。
            Drain(writer: null);
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);

            // append: true + 会话分隔行 —— 断线重连不会把上一次(很可能就是卡死那次)的日志冲掉。
            using var writer = new StreamWriter(_logPath, append: true) { AutoFlush = true };
            writer.WriteLine($"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} 连接 ====");
            Drain(writer);
        }
        catch (Exception)
        {
            Drain(writer: null);   // 文件写不了就只读不记,别因为日志把功能弄坏
        }
    }

    private void Drain(TextWriter? writer)
    {
        var buffer = new byte[1024];
        var pending = new StringBuilder();

        while (!_stop.IsCancellationRequested)
        {
            int read;

            try
            {
                read = _port.Read(buffer, 0, buffer.Length);
            }
            catch (TimeoutException)
            {
                continue;   // 这段时间没有新日志,正常
            }
            catch (Exception)
            {
                return;     // 口关了/线拔了,收工
            }

            if (read <= 0)
            {
                continue;
            }

            pending.Append(Encoding.UTF8.GetString(buffer, 0, read));

            while (true)
            {
                var text = pending.ToString();
                var cut = text.IndexOf('\n');

                if (cut < 0)
                {
                    break;
                }

                var line = text[..cut].TrimEnd('\r');
                pending.Remove(0, cut + 1);

                if (line.Length > 0)
                {
                    writer?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {line}");
                }
            }

            // 设备一直不换行也不能把内存撑爆。
            if (pending.Length > 4096)
            {
                pending.Clear();
            }
        }
    }
}
