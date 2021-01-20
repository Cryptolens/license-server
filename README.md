# License Server

## Idea
**Problem**: Large companies tend to have strict policies that restrict certain machines to have direct internet access. This is a problem if we want license files on them to be up-to-date.

**Solution**: We allow one computer, the *license server*, to have internet access. All the machines in the network will contact the license server and it will in turn contact Cryptolens.

![](example.png)

## Starting the server

In order to launch the server, you need to run `LicenseServer.exe` as an administrator (you can download it [here](https://github.com/Cryptolens/license-server/releases)). The default port is 8080 but this can be changed.
One way of doing it is to run CMD as an administrator and then type the command below:

```
C:\> LicenseServer.exe 5000
```

You can also specify the port inside the application.

> Please make sure to check that this port is open so that other computers in the network can access it (shown below).

## Allowing ports

In order to allow other computers in the network to access the license server, you need to open up the desired port in the firewall. 
If you use the default firewall, it can be opened as follows:

1. Run `wf.msc`
2. Right click on **Inbound Rules** and then click on **Add Rule**
3. Ryle type should be **Port**
4. **Specific local ports** should be checked, and the textbox at the right of it should be set to 5000 (or any other port of your choosing).
5. Click next through the entire wizzard and that should be it.

## Connect to the server

Depending on which of our client SDKs you use, there will be a different way to provide the URL of the license server. For example, if you use the [.NET SDK](https://github.com/Cryptolens/cryptolens-dotnet), every API method (such as Key.Activate) will have an extra parameter `LicenseServerUrl`, which can be used to provide the URL of the license server.

## Enable caching of licenses

Since v1.2 there is a way to cache responses to `Key.Activate`, which helps to reduce the number of requests sent to Cryptolens. This is useful especially if your clients would have temporary internet connectivity issues. To enable license caching, you need to specify how long the license server should store each license. Note: if your application uses the `signatureExpirationInterval` parameter in `HasValidSignature`, the lifetime of the cache on the license server needs to be either equal to `signatureExpirationInterval` or less. Otherwise, your client application will throw an error.

As an example, to launch the server that caches licenses for 10 days, it can be started as follows:

```
C:\> LicenseServer.exe 8080 10
```
