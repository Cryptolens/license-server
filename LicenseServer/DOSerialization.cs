/**
 * Copyright (c) 2019 - 2021 Cryptolens AB
 * To use the license server, a separate subscription is needed. 
 * Pricing information can be found on the following page: https://cryptolens.io/products/license-server/
 * 
 * The method used to store the logs is described in the following report: https://eprint.iacr.org/2021/937
 * 
 * */

using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LicenseServer
{

    public class DOOperations
    {
        public static byte[] AddDataDOOP(DataObjectOperation data, byte[] previousBlock, string RSAPublicKey)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.FromXmlString(RSAPublicKey);

                var obj = new SecureObjectDOOP { Data = data, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), PreviousObjectEncrypted = previousBlock };

                var serializedObj = MessagePackSerializer.Serialize(obj);
                var encryptedObj = AES128Encrypt(serializedObj);

                var unencryptedObject = new UnsecureObject
                {
                    IV = encryptedObj.IV,
                    EncryptedObject = encryptedObj.Encrypted,
                    EncryptedKey = rsa.Encrypt(encryptedObj.Key, RSAEncryptionPadding.Pkcs1)
                };

                return MessagePackSerializer.Serialize(unencryptedObject);
            }
        }

        public static AESEncrypted AES128Encrypt(byte[] data)
        {
            byte[] IV;
            byte[] key;
            byte[] encrypted;

            using (var aes = Aes.Create())
            {
                aes.KeySize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.Zeros;

                aes.GenerateIV();
                aes.GenerateKey();

                IV = aes.IV;
                key = aes.Key;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(data, 0, data.Length);
                        csEncrypt.FlushFinalBlock();

                        encrypted = msEncrypt.ToArray();
                    }
                }

            }

            return new AESEncrypted { Encrypted = encrypted, IV = IV, Key = key };
        }
    }


    public class DOKey : LAKeyBase
    {
        public int ProductId { get; set; }
        public string Key { get; set; }
        public long DOID { get; set; }

    }

    /// <summary>
    /// Information that needs to be secured before written to disk.
    /// </summary>
    [MessagePackObject]
    public class SecureObjectDOOP
    {
        /// <summary>
        /// This is what we want to store in each block.
        /// </summary>
        [Key(0)]
        public DataObjectOperation Data { get; set; }

        [Key(1)]
        public long Timestamp { get; set; }

        /// <summary>
        /// This is the previous object.
        /// </summary>
        [Key(2)]
        public byte[] PreviousObjectEncrypted { get; set; }
    }



    [MessagePackObject]
    public class DataObjectOperation
    {
        [Key(0)]
        public int Increment { get; set; }

        [Key(1)]
        public long DataObjectId { get; set; }
    }

    /// <summary>
    /// Information that can be stored on disk as it is.
    /// </summary>
    [MessagePackObject]
    public class UnsecureObject
    {
        [Key(0)]
        public byte[] IV { get; set; }

        [Key(1)]
        public byte[] EncryptedKey { get; set; }

        [Key(2)]
        public byte[] EncryptedObject { get; set; }
    }

    public class AESEncrypted
    {
        public byte[] Key { get; set; }
        public byte[] IV { get; set; }
        public byte[] Encrypted { get; set; }
    }
}
