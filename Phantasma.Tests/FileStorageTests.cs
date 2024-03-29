﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using static Phantasma.Blockchain.Contracts.Native.EnergyContract;
using static Phantasma.Blockchain.Contracts.Native.StorageContract;

namespace Phantasma.Tests
{
    [TestClass]
    public class FileStorageTests
    {
        #region SuccessTests

        //stake soul and upload a file under the available space limit
        [TestMethod]
        public void SingleUploadSuccess()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakeAmount = BaseEnergyRatioDivisor;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(BaseEnergyRatioDivisor * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize * 2, content, ArchiveFlags.None).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            })
            ;
            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == 0);
        }

        //upload a file for less than available space and perform partial unstake
        [TestMethod]
        public void ReduceAvailableSpace()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakedAmount = BaseEnergyRatioDivisor * 5;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakedAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Upload a file
            var filename = "notAVirus.exe";

            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(stakedAmount * KilobytesPerStake * 1024 / 5) - (long)headerSize;
            var content = new byte[contentSize];

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == contentSize);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //-----------
            //Try partial unstake: should succeed
            var initialStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            var stakeReduction = stakedAmount / 5;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Unstake", testUser.Address, stakeReduction).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var finalStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(finalStakedAmount == 0);
        }

        //upload a file for full space, delete file and perform full unstake
        [TestMethod]
        public void UnstakeAfterUsedSpaceRelease()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakedAmount = BaseEnergyRatioDivisor;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakedAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Upload a file
            var filename = "notAVirus.exe";

            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(stakedAmount * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == contentSize);

            //-----------
            //Delete the file

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "DeleteFile", testUser.Address, filename).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == 0);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //-----------
            //Try to unstake everything: should succeed
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Unstake", testUser.Address, stakedAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var finalStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(finalStakedAmount == 0);

        }

        //upload more than one file for a total size that is less than the available space
        [TestMethod]
        public void CumulativeUploadSuccess()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var stakeAmount = BaseEnergyRatioDivisor * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(stakeAmount * KilobytesPerStake * 1024 / 4) - (long)headerSize;
            var content = new byte[contentSize];

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            var oldSpace = contentSize + headerSize;
            //----------
            //Upload another file: should succeed

            filename = "giftFromTroia.exe";
            headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            contentSize = (long)(stakeAmount * KilobytesPerStake * 1024 / 4) - (long)headerSize;
            content = new byte[contentSize];

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            Assert.IsTrue(usedSpace == oldSpace + contentSize + headerSize);

            oldSpace = contentSize + headerSize;
            //----------
            //Upload another file: should succeed

            filename = "JimTheEarthWORM.exe";
            headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            contentSize = (long)(stakeAmount * KilobytesPerStake * 1024 / 4) - (long)headerSize;
            content = new byte[contentSize];

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            Assert.IsTrue(usedSpace == oldSpace + contentSize + headerSize);
        }

        //reupload a file maintaining the same name after deleting the original one
        [TestMethod]
        public void ReuploadSuccessAfterDelete()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var stakeAmount = BaseEnergyRatioDivisor * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(stakeAmount * KilobytesPerStake * 1024 / 2) - (long)headerSize;
            var content = new byte[contentSize];

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            var oldSpace = contentSize + headerSize;

            //-----------
            //Delete the file

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "DeleteFile", testUser.Address, filename).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == 0);

            //----------
            //Upload the same file: should succeed
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            Assert.IsTrue(usedSpace == oldSpace);
        }

        //upload a duplicate of an already uploaded file but by a different owner
        [TestMethod]
        public void UploadDuplicateFileDifferentOwner()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUserA = KeyPair.Generate();
            var testUserB = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for userA
            var stakeAmount = BaseEnergyRatioDivisor * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUserA.Address, stakeAmount).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUserA.Address);
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //----------
            //Perform a valid Stake call for userB
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUserA.Address, stakeAmount).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUserA.Address);
            Assert.IsTrue(stakedAmount == stakeAmount);

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //User A uploads a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(stakeAmount * KilobytesPerStake * 1024 / 2) - (long)headerSize;
            var content = new byte[contentSize];

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUserA.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUserA.Address);

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            //----------
            //User B uploads the same file: should succeed
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUserB.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            Assert.IsTrue(usedSpace == contentSize + headerSize);
        }
        #endregion

        #region FailureTests

        //try unstaking below required space for currently uploaded files
        [TestMethod]
        public void UnstakeWithStoredFilesFailure()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakedAmount = BaseEnergyRatioDivisor;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakedAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Upload a file
            var filename = "notAVirus.exe";

            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(stakedAmount * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == contentSize);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //-----------
            //Try to unstake everything: should fail due to files still existing for this user
            var initialStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            var stakeReduction = initialStakedAmount - BaseEnergyRatioDivisor;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Unstake", testUser.Address, stakeReduction).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            var finalStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(initialStakedAmount == finalStakedAmount);

            usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);
        }

        //try to upload a single file beyond available space
        [TestMethod]
        public void UploadBeyondAvailableSpace()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakeAmount = BaseEnergyRatioDivisor;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should fail due to exceeding available space
            var filename = "notAVirus.exe";
            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(stakeAmount * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize * 2, content, ArchiveFlags.None).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            })
            ;
            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == 0);
        }

        //try to upload multiple files that individually dont go above available space, but that cumulatively do so
        [TestMethod]
        public void CumulativeUploadMoreThanAvailable()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakeAmount = BaseEnergyRatioDivisor;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(stakedAmount * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            var oldSpace = contentSize + headerSize;
            //----------
            //Upload a file: should fail due to exceeding available storage capacity

            filename = "giftFromTroia.exe";
            headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            contentSize = (long)(stakedAmount * KilobytesPerStake * 1024) - (long)headerSize;
            content = new byte[contentSize];

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            })
                ;

            Assert.IsTrue(usedSpace == oldSpace);
        }

        //upload a file with the same name as an already uploaded file
        [TestMethod]
        public void UploadDuplicateFilename()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var stakeAmount = BaseEnergyRatioDivisor * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(stakeAmount * KilobytesPerStake * 1024 / 2) - (long)headerSize;
            var content = new byte[contentSize];

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            var oldSpace = contentSize + headerSize;

            //----------
            //Upload a file with the same name: should fail
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            Assert.IsTrue(usedSpace == oldSpace);
        }


        #endregion


    }
}
