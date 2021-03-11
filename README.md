# License Server

## Idea
**Problem**: Large companies tend to have strict policies that restrict certain machines to have direct internet access. This is a problem if we want license files on them to be up-to-date.

**Solution**: We allow one computer, the *license server*, to have internet access. All the machines in the network will contact the license server and it will in turn contact Cryptolens. Alternatively, the *license server* can store a copy of the license files and work offline.

![](example.png)

## Starting the server

In order to launch the server, you need to run `LicenseServer.exe` as an administrator (you can download it [here](https://github.com/Cryptolens/license-server/releases)). The default port is 8080 but this can be changed.
One way of doing it is to run CMD as an administrator and then type the command below:

```
C:\> LicenseServer.exe 5000
```

You can also specify the port inside the application.

> Please make sure to check that this port is open so that other computers in the network can access it (shown below).

### Running on Linux and Mac
To run the license server on either Linux or Mac, you need to make sure that .NET 5 runtime is installed (read more [here](https://dotnet.microsoft.com/download/dotnet/5.0)). Once it is installed, the license server can be started as follows:

```
dotnet LicenseServer.dll
```

In the rest of the article, you can just need to add `dotnet` before launching the server. Everything else is the same. Based on our tests, no sudo access is needed to run the license server on Linux.

## Allowing ports (Windows only)

In order to allow other computers in the network to access the license server, you need to open up the desired port in the firewall. 
If you use the default firewall, it can be opened as follows:

1. Run `wf.msc`
2. Right click on **Inbound Rules** and then click on **Add Rule**
3. Ryle type should be **Port**
4. **Specific local ports** should be checked, and the textbox at the right of it should be set to 5000 (or any other port of your choosing).
5. Click next through the entire wizzard and that should be it.

## Connect to the server

Depending on which of our client SDKs you use, there will be a different way to provide the URL of the license server. For example, if you use the [.NET SDK](https://github.com/Cryptolens/cryptolens-dotnet), every API method (such as Key.Activate) will have an extra parameter `LicenseServerUrl`, which can be used to provide the URL of the license server.

## Additional features

There are two versions of the license server, `v1.*` and `v2.*`. If you only need the request forwarding feature, `v.1.*` version will suffice. If you want to use the extra features listed below, you can use `v2.*` version instead.

### Enable caching of licenses
Since v2.0 there is a way to cache responses to `Key.Activate`, which helps to reduce the number of requests sent to Cryptolens. This is useful especially if your clients would have temporary internet connectivity issues. To enable license caching, you need to specify how long the license server should store each license. Note: if your application uses the `signatureExpirationInterval` parameter in `HasValidSignature`, the lifetime of the cache on the license server needs to be either equal to `signatureExpirationInterval` or less. Otherwise, your client application will throw an error.

As an example, to launch the server that caches licenses for 10 days, it can be started as follows:

```
C:\> LicenseServer.exe 8080 10
```

#### Customers who are permanently offline
The default behaviour of the server is to *always* attempt to get the latest copy of the license. However, if you know that the license server will not have access to the internet, it is better to enable the *offline mode* so that the license server always reads from cache.

To enable offline mode, you can launch the server as follows:

```
C:\> LicenseServer.exe 8080 10 work-offline
```

In this case, it is a good idea to provide the license files (aka. activation files) that you want to load into the server. You only need to do it once or when the cache needs to be updated. If the license file (with a `.skm` extension) is in the *Downloads* folder, it can be loaded as follows:

```
C:\> LicenseServer.exe 8080 10 work-offline "C:\Users\User Name\Downloads"
```

If you want to load a specific list of files and folders, they can be separated by a semi-colon ';'.
```
C:\> LicenseServer.exe 8080 10 work-offline "C:\Users\User Name\Downloads";"C:\temp\file.skm"
```

### Loading settings from a config file
To make it easier to deploy the license server on customer site, you can add all settings into `config.json` in the same folder as the server. The structure of the configuration file is shown below:

```
{
    "Port" : 8080,
    "CacheLength": 365,
    "OfflineMode" : True,
    "ActivationFiles" : ["C:\Users\User Name\Downloads"]
}
```

The `ActivationFiles` can either reference a specific file or a folder. If it references a folder, all files with the `.skm` extension will be loaded.