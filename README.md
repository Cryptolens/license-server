# License Server

> **Note**: a seprate subscription is required to use the license server: https://cryptolens.io/products/license-server/

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
var result = Key.Activate(token: auth, productId: 3349, key: "GEBNC-WZZJD-VJIHG-GCMVD", machineCode: "foo");

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
