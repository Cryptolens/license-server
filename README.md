# License Server

> **Note**: a seprate subscription is required to use the license server: https://cryptolens.io/products/license-server/

## Idea
**Problem**: Large companies tend to have strict policies that restrict certain machines to have direct internet access. This is a problem if we want license files on them to be up-to-date.

**Solution**: We allow one computer, the *license server*, to have internet access. All the machines in the network will contact the license server and it will in turn contact Cryptolens. Alternatively, the *license server* can store a copy of the license files and work offline.

![](example.png)

## Getting started
Since v2.2, the license server needs to be compiled on your end to create the binaries. All configuration is stored inside the `ConfigurationFromCryptolens` variable in `Program.cs`, which can be created on [this page](https://app.cryptolens.io/extensions/LicenseServer?OfflineMode=False&LocalFloatingServer=False). In other words, there is no need to provide any arguments when calling the license server or use an external configuration file.

The license server can be compiled on most operating systems and the process is as follows:

### Install .NET
To install .NET, visit https://dotnet.microsoft.com/en-us/download/dotnet/6.0 and download the SDK (i.e. not the runtime).

### Configuring the server
There are two steps involved:

1. Visit [the configuration page](https://app.cryptolens.io/extensions/LicenseServer?OfflineMode=False&LocalFloatingServer=False) to create a configuration that will make the server work in standard mode.
2. Copy the configuration string and paste it in the `ConfigurationFromCryptolens` variable in `Program.cs`.
3. Environment variables can also be used to store configuration data. Please read more [here](#alternative-ways-to-configure-the-server).

Later in this tutorial, there are examples of calling the license server using command line arguments. We recommend to use the configuration string as described in (1) if possible. If you have any questions, please reach out to us at support@cryptolens.io.

### Building the server
To build the server, you can run the following command in the folder that contains the `LicenseServerCore.sln` file:

```
dotnet build LicenseServerCore.sln --configuration Release
```

From now on, you can use the instructions further down in this page to launch the executable.

## Starting the server

In order to launch the server, you need to run `LicenseServer.exe` as an administrator (you can download it [here](https://github.com/Cryptolens/license-server/releases)). The default port is 8080 but this can be changed.
One way of doing it is to run CMD as an administrator and then type the command below:

```
C:\> LicenseServer.exe 5000
```

For newer versions of the license server, you can [this configuration](https://app.cryptolens.io/extensions/LicenseServer?OfflineMode=False&LocalFloatingServer=False&Port=5000) to set the port to 5000.

> Please make sure to check that this port is open so that other computers in the network can access it (shown below).

### Running as a Windows service
If you would like to run the license server as a service on Windows, you can accomplish that as described [here](#running-the-license-server-as-a-service).

### Running on Linux and Mac
To run the license server on either Linux or Mac, you need to make sure that .NET 5 runtime or later is installed (read more [here](https://dotnet.microsoft.com/download/dotnet/5.0)). Once it is installed, the license server can be started as follows:

```
dotnet LicenseServer.dll
```

In the rest of the article, you just need to add `dotnet` before launching the server. Everything else is the same. Based on our tests, no sudo access is needed to run the license server on Linux.

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

For newer versions of the license server, you can use [this configuration](https://app.cryptolens.io/extensions/LicenseServer?OfflineMode=False&LocalFloatingServer=False&CacheLength=10&Port=8080) instead.

#### Customers who are permanently offline
The default behaviour of the server is to *always* attempt to get the latest copy of the license. However, if you know that the license server will not have access to the internet, it is better to enable the *offline mode* so that the license server always reads from cache.

To enable offline mode, you can launch the server as follows (or use [this configuration string](https://app.cryptolens.io/extensions/LicenseServer?OfflineMode=True&LocalFloatingServer=False&CacheLength=10&Port=8080)):

```
C:\> LicenseServer.exe 8080 10 work-offline
```

In this case, it is a good idea to provide the license files (aka. activation files) that you want to load into the server. You only need to do it once or when the cache needs to be updated. If the license file (with a `.skm` extension) is in the *Downloads* folder, it can be loaded as follows (or use [this configuration string](https://app.cryptolens.io/extensions/LicenseServer?OfflineMode=True&LocalFloatingServer=False&CacheLength=10&Port=8080&ActivationFileFolder=C:\Users\User%20Name\Downloads)):

```
C:\> LicenseServer.exe 8080 10 work-offline "C:\Users\User Name\Downloads"
```

If you want to load a specific list of files and folders, they can be separated by a semi-colon ';'.
```
C:\> LicenseServer.exe 8080 10 work-offline "C:\Users\User Name\Downloads";"C:\temp\file.skm"
```

##### Floating licenses offline
If you want to use [floating licensing](https://help.cryptolens.io/licensing-models/floating) in offline mode (for example, to restrict the maximum number of containers a user can start), it can be done as follows:

1. Visit [https://app.cryptolens.io/extensions/licenseserver](https://app.cryptolens.io/extensions/licenseserver) and copy the "License server configuration" and "RSA Public Key".
2. When verifying the signature inside your application, please use the RSA Public Key on this page instead of the one you would normally use when your application can access our API. This key will only work with the license server that uses the configuration above.
3. In the license server project, paste the value of "license server configuration" to `ConfigurationFromCryptolens` variable in `Program.cs`.
4. Compile the license server in release mode.
5. In the release folder, create a new folder called "licensefiles".
6. Visit the [product page](https://app.cryptolens.io/Product) and click on the yellow button next to the license key that belongs to your client (to manage all activations). Now, click on "Download activation file" and put this file into the "licensefiles" folder created earlier.
7. You can now send the license server (in the release folder, including all the files and folders) to your client.

> **Note (for .NET users)** For the time being, `Key.Activate` needs to be called the same way as the in the Unity example: https://help.cryptolens.io/getting-started/unity

```cs
// call to activate
var result = Key.Activate(token: auth, productId: 3349, key: "GEBNC-WZZJD-VJIHG-GCMVD", machineCode: "foo", floatingTimeInterval: 150, LicenseServerUrl: "http://192.168.0.2:8080");

// obtaining the license key (and verifying the signature automatically).
var license = LicenseKey.FromResponse("RSAPubKey", result);
```
> **Note** If the local license server is enabled, floating license status will not be synchronized with Cryptolens, even if online mode is enabled. Moreover, GetKey request will return the information stored on the local license server and sign it using the local license server's private key. This means that if you have enabled floating licensing offline, you need to use public key that was shown on the [configuration page](https://app.cryptolens.io/extensions/LicenseServer) for both `Activate` and `GetKey` requests.

If you need to deactivate machine earlier, you can use the Deactivate method with `Floating=true`, similar to the way it would have been done when calling Cryptolens' Web API:

```cs
var result = Key.Deactivate("", new DeactivateModel { ProductId = 3349, Key = "key", MachineCode = "machine", LicenseServerUrl = "http://192.168.0.2:8080", Floating = true });
```

To obtain the list of all activated floating machines, you can use the GetKey call:

```cs
var result = Key.GetKey(token: "", productId: 3349, key: "", LicenseServerUrl: "http://192.168.0.2:8080");

// obtaining the license key (and verifying the signature automatically).
var license = LicenseKey.FromResponse("RSAPubKey", result);
```

##### Usage-based licensing offline
If the license server is set to work offline, it is still possible to collect information about usage (that is stored in data objects) and bill your clients for it.
At the time of writing, only data objects associated with a license key can be used.

To get started, please follow the same steps as described in the [floating license offline](#floating-licenses-offline) section. When creating the configuration file, please make sure that _offline mode_ is enabled.

When this is done, all usage information will be stored in the "usage" folder. The structure of the logs is described here: [https://eprint.iacr.org/2021/937](https://eprint.iacr.org/2021/937)

> **Note:** The license file in the `licensefiles` folder needs to have the data objects that will be incremented or decremented. Otherwise, the license server will throw an error.


### Loading settings from a config file

> **Note** In newer versions of the license server, we recommend to create a [configuration string](https://app.cryptolens.io/extensions/LicenseServer?OfflineMode=False&LocalFloatingServer=False) as described in the beginning of this page.

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


### Running the license server as a service
The license server can run as a Windows service in the background. This can be accomplished as follows (using [sc](https://docs.microsoft.com/en-us/windows-server/administration/windows-commands/sc-create)). Note, these commands need to be ran as an Administrator:

```
sc create license-server binpath="D:\path\to\licenseserver\LicenseServer.exe" start=auto
net start license-server
```

Note: the path to the license server needs to be absolute. Furthermore, it is important that the `ConfigurationFromCryptolens` variable is not empty and uses your own configuration. The configuration can be obtained on [https://app.cryptolens.io/extensions/licenseserver](https://app.cryptolens.io/extensions/licenseserver). When creating a new configuration, please set **Activation file folder** to an absolute path. For example, **C:\license-files**. 

We have tested the license server version that targets .NET Framework 4.6.1.

Below are other useful commands:
```
sc stop license-server
sc queryex license-server
sc delete  license-server
```

If you need any help, please let us know at support@cryptolens.io.

### Alternative ways to configure the server

It is also possible to configure the license server using environment variables. For now, the license server will only read the environment variables in two cases:

1. If the `ConfigurationFromCryptolens` in Program.cs is not null or empty.
2. If license server runs as a service (Windows).

For example, if you prefer to use the environment variables, you can set `ConfigurationFromCryptolens` to any string value and then rely on the environment variables. Alternatively, you can create a configuration string at https://app.cryptolens.io/extensions/licenseserver and then set "path to config file" to `USE_ENVIRONMENT_VARIABLES`.

Cryptolens uses the following environment variables:

| Name   | Description      |
|----------|-------------|
| `cryptolens_offlinemode` | Specifies if the license server should contact the central server (if set to false) or rely on the cached version if such exists (if set to true). When set to true, the license server will at first try the cache before attempting to contact the license server. |
| `cryptolens_port` | The port to use. |
| `cryptolens_activationfilefolder` | The path to the folder with activation files. Please set it to an absolute path when running the license server as a service |
| `cryptolens_cachelength` | The amount of days until a new license file should be obtained. |
| `cryptolens_pathtoconfigfile` | The path to the configuration file. This can be useful if you anticipate that your clients might need to change certain properties more often, and then it may be easier to change the file rather than restarting the machine (which is often required for the environment variables to take effect). For now, you can set the port and the folder to the activation files.|
| `cryptolens_cachefolder` | Path to the cache folder |

