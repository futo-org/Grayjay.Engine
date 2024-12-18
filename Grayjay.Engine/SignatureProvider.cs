using System;
using System.Security.Cryptography;
using System.Text;

namespace Grayjay.Engine
{
    public static class SignatureProvider
    {
        public class KeyPair
        {
            public string PrivateKey { get; }
            public string PublicKey { get; }

            public KeyPair(string privateKey, string publicKey)
            {
                PrivateKey = privateKey;
                PublicKey = publicKey;
            }
        }

        public static string Sign(string text, string privateKey)
        {
            try
            {
                byte[] privateKeyBytes = Convert.FromBase64String(privateKey);
                using RSA rsa = RSA.Create();
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);

                byte[] dataBytes = Encoding.UTF8.GetBytes(text);
                byte[] signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);

                return Convert.ToBase64String(signatureBytes);
            }
            catch (Exception ex)
            {
                throw new Exception("Error while signing data", ex);
            }
        }

        public static bool Verify(string text, string signature, string publicKey)
        {
            try
            {
                byte[] publicKeyBytes = Convert.FromBase64String(publicKey);
                using RSA rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

                byte[] signatureBytes = Convert.FromBase64String(signature);
                byte[] dataBytes = Encoding.UTF8.GetBytes(text);
                return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
            }
            catch (Exception ex)
            {
                throw new Exception("Error while verifying signature", ex);
            }
        }

        public static KeyPair GenerateKeyPair()
        {
            try
            {
                using RSA rsa = RSA.Create();
                rsa.KeySize = 2048;

                byte[] privateKeyBytes = rsa.ExportPkcs8PrivateKey();
                string privateKey = Convert.ToBase64String(privateKeyBytes);

                byte[] publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
                string publicKey = Convert.ToBase64String(publicKeyBytes);

                return new KeyPair(privateKey, publicKey);
            }
            catch (Exception ex)
            {
                throw new Exception("Error while generating key pair", ex);
            }
        }
    }
}