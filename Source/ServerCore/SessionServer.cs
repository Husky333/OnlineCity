﻿using Model;
using OCUnion;
using ServerOnlineCity.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Transfer;
using Util;

namespace ServerOnlineCity
{
    public class SessionServer : IDisposable
    {
        private ConnectClient Client;
        private byte[] Key;
        private static Random Rnd = new Random();
        private static Encoding KeyEncoding = Encoding.GetEncoding(1252);
        private CryptoProvider cryptoHash = new CryptoProvider();
        private Service Worker;
        private DateTime ServiceCheckTime;

        public void Dispose()
        {
            Client.Dispose();
        }

        private void SetKey()
        {
            var rnd = new Random();
            var k = new byte[Rnd.Next(400, 600)];
            for (int i = 0; i < k.Length; i++)
            {
                k[i] = (byte)(Rnd.Next(0, 128) + rnd.Next(0, 128));
            }
            var k2 = KeyEncoding.GetBytes("089~`tgjРР·dfgорЫГ9♫7ПМпfghjp147&$#hf%#h^^gxчмиА▀ЫЮББЮю,><2en]√");

            int oldLen = k.Length;
            Array.Resize(ref k, k.Length + k2.Length);
            Array.Copy(k2, 0, k, oldLen, k2.Length);

            Key = cryptoHash.GetHash(k);
        }

        public void Do(ConnectClient client)
        {
            Client = client;

            Loger.Log("Server ReceiveBytes1");

            ///установка условно защищенного соединения
            //Строго первый пакет: Передаем серверу КОткр
            var rc = Client.ReceiveBytes();
            var crypto = new CryptoProvider();
            if (SessionClient.UseCryptoKeys) crypto.OpenKey = Encoding.UTF8.GetString(rc);

            //Строго первый ответ: Передаем клиенту КОткр(Сессия)
            SetKey();
            Loger.Log("Server SendMessage1");
            if (SessionClient.UseCryptoKeys)
                Client.SendMessage(crypto.Encrypt(Key));
            else
                Client.SendMessage(Key);

            var context = new ServiceContext();
            Worker = new Service(context);

            ///рабочий цикл
            while (true)
            {
                var rec = Client.ReceiveBytes();
                if (context.Player != null) context.Player.Public.LastOnlineTime = DateTime.UtcNow;

                //отдельно обрабатываем пинг
                if (rec.Length == 1)
                {
                    if (rec[0] == 0x00)
                    {
                        Client.SendMessage(new byte[1] { 0x00 });
                    }
                    //отдельно обрабатываем запрос на обновление (ответ 0 - нет ничего, 1 - что-то есть) 
                    else if (rec[0] == 0x01)
                    {
                        var exists = ServiceCheck();
                        Client.SendMessage(new byte[1] { exists ? (byte)0x01 : (byte)0x00 });
                    }
                    continue;
                }

                var rec2 = CryptoProvider.SymmetricDecrypt(rec, Key);
                var recObj = (ModelContainer)GZip.UnzipObjByte(rec2); //Deserialize
                var sendObj = Worker.GetPackage(recObj);
                var ob = GZip.ZipObjByte(sendObj); //Serialize
                var send = CryptoProvider.SymmetricEncrypt(ob, Key);
                
                Client.SendMessage(send);
            }
        }

        /// <summary>
        /// Есть ли изменеия. Сейчас используется только для чата
        /// </summary>
        /// <returns></returns>
        private bool ServiceCheck()
        {
            if (ServiceCheckTime == DateTime.MinValue)
            {
                ServiceCheckTime = DateTime.UtcNow;
                return true;
            }

            //На данный момен только проверка чата
            var res = Worker.CheckChat(ServiceCheckTime);
            ServiceCheckTime = DateTime.UtcNow;
            return res;
        }
    }
}