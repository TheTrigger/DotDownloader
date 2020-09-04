# Dot Downloader

[![Codacy Badge](https://api.codacy.com/project/badge/Grade/e7cbcd57f92a485bb26be7788cc9f2c2)](https://app.codacy.com/manual/TheTrigger/DotDownloader?utm_source=github.com&utm_medium=referral&utm_content=TheTrigger/DotDownloader&utm_campaign=Badge_Grade_Settings)
[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Oibi.Downloader)](https://www.nuget.org/packages/Oibi.Downloader/)

A small stateless utility to download remote files without external dependencies

## Features

- [x] concurrent split download
- [x] resume downloads
- [x] progress
- [x] abort (`CancellationToken`)
- [x] stateless
- [ ] speed meter
- [ ] pause/resume
- [ ] proxy pool
- [ ] throttling
- ~~[ ] contentMD5 verify~~

## TODO list

- [ ] more refactoring
- [ ] implement missing features
- [ ] xunit tests 🤔

## MaybeBoard: Download manager

- [ ] priorities
- [ ] pool with semaphores

NB. disk's space is preallocated

## Getting started

Install Package

```ps
Install-Package Oibi.Downloader
```

```Csharp
using Oibi.Download;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Oibi.Downloader.Demo
{
    internal class Program
    {
        private static async Task Main()
        {
            var settings = new FileDownloadSettings
            {
                RemoteResource = new Uri("http://test.kpnqwest.it/file2000.bin"),
                LocalResource = new FileInfo(@"C:\Users\Fabio\Downloads\.downloader\2GB.bin")
            };

            var dl = new DotDownloader(settings);
            var task = dl.DownloadAsync();

            do
            {
                await Task.Delay(111);
                Console.Write($"Progress: {dl.Progress * 100:0.00}%\r");

            } while (dl.Progress != 1d);

            Console.WriteLine("DOWNLOAD COMPLETED");

            await task;
        }
    }
}
```
