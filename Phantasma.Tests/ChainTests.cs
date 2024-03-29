using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;

using Phantasma.Blockchain;
using Phantasma.Storage.Context;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Blockchain.Utils;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Tokens;

namespace Phantasma.Tests
{
    [TestClass]
    public class ChainTests
    {
        [TestMethod]
        public void Decimals()
        {
            var places = 8;
            decimal d = 93000000;
            BigInteger n = 9300000000000000;

            var tmp1 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(n, places), places);

            Assert.IsTrue(n == tmp1);
            Assert.IsTrue(d == UnitConversion.ToDecimal(UnitConversion.ToBigInteger(d, places), places));

            Assert.IsTrue(d == UnitConversion.ToDecimal(n, places));
            Assert.IsTrue(n == UnitConversion.ToBigInteger(d, places));

            var tmp2 = UnitConversion.ToBigInteger(0.1m, Nexus.FuelTokenDecimals);
            Assert.IsTrue(tmp2 > 0);

            decimal eos = 1006245120;
            var tmp3 = UnitConversion.ToBigInteger(eos, 18);
            var dec = UnitConversion.ToDecimal(tmp3, 18);
            Assert.IsTrue(dec == eos);

            BigInteger small = 60;
            var tmp4 = UnitConversion.ToDecimal(small, 10);
            var dec2 = 0.000000006m;
            Assert.IsTrue(dec2 == tmp4);
        }

        [TestMethod]
        public void GenesisBlock()
        {
            var owner = KeyPair.Generate();
            var nexus = new Nexus();

            Assert.IsTrue(nexus.CreateGenesisBlock("simnet", owner, DateTime.Now));

            Assert.IsTrue(nexus.GenesisHash != Hash.Null);

            var rootChain = nexus.RootChain;

            var symbol = Nexus.FuelTokenSymbol;
            Assert.IsTrue(nexus.TokenExists(symbol));
            var token = nexus.GetTokenInfo(symbol);
            Assert.IsTrue(token.MaxSupply > 0);

            var supply = nexus.GetTokenSupply(rootChain.Storage, symbol);
            Assert.IsTrue(supply > 0);

            Assert.IsTrue(rootChain != null);
            Assert.IsTrue(rootChain.BlockHeight > 0);

            var children = nexus.GetChildChainsByName(rootChain.Name);
            Assert.IsTrue(children.Any());

            Assert.IsTrue(nexus.IsValidator(owner.Address));

            var randomKey = KeyPair.Generate();
            Assert.IsFalse(nexus.IsValidator(randomKey.Address));

            /*var txCount = nexus.GetTotalTransactionCount();
            Assert.IsTrue(txCount > 0);*/
        }

        [TestMethod]
        public void FungibleTokenTransfer()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var accountChain = nexus.FindChainByName("account");
            var symbol = Nexus.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(symbol);

            var testUser = KeyPair.Generate();

            var amount = UnitConversion.ToBigInteger(2, token.Decimals);

            var oldBalance = nexus.RootChain.GetTokenBalance(symbol, owner.Address);

            Assert.IsTrue(oldBalance > amount);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(symbol, testUser.Address);
            Assert.IsTrue(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(symbol, owner.Address);
            var gasFee = nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(transferBalance + newBalance + gasFee == oldBalance);
        }

        [TestMethod]
        public void CreateToken()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var accountChain = nexus.FindChainByName("account");
            var symbol = "BLA";

            var tokenSupply = UnitConversion.ToBigInteger(10000, 18);
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "BlaToken", tokenSupply, 18, TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite | TokenFlags.Divisible);
            simulator.MintTokens(owner, symbol, tokenSupply);
            simulator.EndBlock();

            var token = nexus.GetTokenInfo(symbol);

            var testUser = KeyPair.Generate();

            var amount = UnitConversion.ToBigInteger(2, token.Decimals);

            var oldBalance = nexus.RootChain.GetTokenBalance(symbol, owner.Address);

            Assert.IsTrue(oldBalance > amount);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(symbol, testUser.Address);
            Assert.IsTrue(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(symbol, owner.Address);

            Assert.IsTrue(transferBalance + newBalance == oldBalance);
        }

        [TestMethod]
        public void CreateNonDivisibleToken()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var accountChain = nexus.FindChainByName("account");
            var symbol = "BLA";

            var tokenSupply = UnitConversion.ToBigInteger(100000000, 18);
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "BlaToken", tokenSupply, 0, TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
            simulator.MintTokens(owner, symbol, tokenSupply);
            simulator.EndBlock();

            var token = nexus.GetTokenInfo(symbol);

            var testUser = KeyPair.Generate();

            var amount = UnitConversion.ToBigInteger(2, token.Decimals);

            var oldBalance = nexus.RootChain.GetTokenBalance(symbol, owner.Address);

            Assert.IsTrue(oldBalance > amount);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(symbol, testUser.Address);
            Assert.IsTrue(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(symbol, owner.Address);

            Assert.IsTrue(transferBalance + newBalance == oldBalance);
        }

        [TestMethod]
        public void AccountRegister()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var symbol = Nexus.FuelTokenSymbol;

            Func<KeyPair, string, bool> registerName = (keypair, name) =>
            {
                bool result = true;

                try
                {
                    simulator.BeginBlock();
                    var tx = simulator.GenerateAccountRegistration(keypair, name);
                    var lastBlock = simulator.EndBlock().FirstOrDefault();

                    if (lastBlock != null)
                    {
                        Assert.IsTrue(tx != null);

                        var evts = lastBlock.GetEventsForTransaction(tx.Hash);
                        Assert.IsTrue(evts.Any(x => x.Kind == Blockchain.Contracts.EventKind.AddressRegister));
                    }
                }
                catch (Exception)
                {
                    result = false;
                }

                return result;
            };

            var testUser = KeyPair.Generate();

            var token = nexus.GetTokenInfo(symbol);
            var amount = UnitConversion.ToBigInteger(10, token.Decimals);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(symbol, testUser.Address);
            Assert.IsTrue(balance == amount);

            var targetName = "hello";
            Assert.IsTrue(targetName == targetName.ToLower());

            Assert.IsFalse(registerName(testUser, targetName.Substring(3)));
            Assert.IsFalse(registerName(testUser, targetName.ToUpper()));
            Assert.IsFalse(registerName(testUser, targetName + "!"));
            Assert.IsTrue(registerName(testUser, targetName));

            var currentName = nexus.LookUpAddressName(testUser.Address);
            Assert.IsTrue(currentName == targetName);

            var someAddress = nexus.LookUpName(targetName);
            Assert.IsTrue(someAddress == testUser.Address);

            Assert.IsFalse(registerName(testUser, "other"));
        }

        [TestMethod]
        public void SimpleTransfer()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;

            var testUserA = KeyPair.Generate();
            var testUserB = KeyPair.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10, Nexus.FuelTokenDecimals);
            var transferAmount = UnitConversion.ToBigInteger(10, Nexus.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, Nexus.FuelTokenSymbol, fuelAmount);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, Nexus.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();

            // Send from user A to user B
            simulator.BeginBlock();
            simulator.GenerateTransfer(testUserA, testUserB.Address, nexus.RootChain, Nexus.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();

            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserB.Address);
            Assert.IsTrue(finalBalance == transferAmount);
        }

        [TestMethod]
        public void SimpleSwap()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;

            var testUserA = KeyPair.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10, Nexus.FuelTokenDecimals);
            var transferAmount = UnitConversion.ToBigInteger(10, Nexus.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, Nexus.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();

            var originalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUserA.Address);

            var swapAmount = UnitConversion.ToBigInteger(0.1m, Nexus.StakingTokenDecimals);
            simulator.BeginBlock();
            simulator.GenerateSwap(testUserA, nexus.RootChain, Nexus.StakingTokenSymbol, Nexus.FuelTokenSymbol, swapAmount);
            simulator.EndBlock();

            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUserA.Address);
            Assert.IsTrue(finalBalance > originalBalance);
        }

        [TestMethod]
        public void TransferToAccountName()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var symbol = Nexus.FuelTokenSymbol;

            Func<KeyPair, string, bool> registerName = (keypair, name) =>
            {
                bool result = true;

                try
                {
                    simulator.BeginBlock();
                    var tx = simulator.GenerateAccountRegistration(keypair, name);
                    var lastBlock = simulator.EndBlock().FirstOrDefault();

                    if (lastBlock != null)
                    {
                        Assert.IsTrue(tx != null);

                        var evts = lastBlock.GetEventsForTransaction(tx.Hash);
                        Assert.IsTrue(evts.Any(x => x.Kind == Blockchain.Contracts.EventKind.AddressRegister));
                    }
                }
                catch (Exception)
                {
                    result = false;
                }

                return result;
            };

            var targetName = "hello";
            var testUser = KeyPair.Generate();
            var token = nexus.GetTokenInfo(symbol);
            var amount = UnitConversion.ToBigInteger(10, token.Decimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            Assert.IsTrue(registerName(testUser, targetName));

            // Send from Genesis address to test user
            var transferAmount = 1;

            var initialFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(symbol, testUser.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, () =>
                ScriptUtils.BeginScript().AllowGas(owner.Address, Address.Null, 1, 9999)
                    .CallContract("token", "TransferTokens", owner.Address, targetName, token.Symbol, transferAmount)
                    .SpendGas(owner.Address).EndScript());
            simulator.EndBlock();

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(symbol, testUser.Address);

            Assert.IsTrue(finalFuelBalance - initialFuelBalance == transferAmount);
        }

        [TestMethod]
        public void SideChainTransferDifferentAccounts()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.FindChainByName("privacy");

            var symbol = Nexus.FuelTokenSymbol;

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            var token = nexus.GetTokenInfo(symbol);
            var originalAmount = UnitConversion.ToBigInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(symbol, sender.Address);
            Assert.IsTrue(balance == originalAmount);

            var crossFee = UnitConversion.ToBigInteger(0.001m, token.Decimals);

            // do a side chain send using test user balance from root to account chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, receiver.Address, targetChain, sideAmount, crossFee);
            simulator.EndBlock();
            var blockA = nexus.RootChain.LastBlock;

            // finish the chain transfer
            simulator.BeginBlock();
            var txB = simulator.GenerateSideChainSettlement(receiver, nexus.RootChain, targetChain, blockA.Hash);
            Assert.IsTrue(simulator.EndBlock().Any());

            // verify balances
            var feeB = targetChain.GetTransactionFee(txB);
            balance = targetChain.GetTokenBalance(symbol, receiver.Address);
            Assert.IsTrue(balance == sideAmount - feeB);

            var feeA = sourceChain.GetTransactionFee(txA);
            var leftoverAmount = originalAmount - (sideAmount + feeA + crossFee);

            balance = sourceChain.GetTokenBalance(symbol, sender.Address);
            Assert.IsTrue(balance == leftoverAmount);
        }

        [TestMethod]
        public void SideChainTransferSameAccount()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.FindChainByName("privacy");

            var symbol = Nexus.FuelTokenSymbol;

            var sender = KeyPair.Generate();

            var token = nexus.GetTokenInfo(symbol);
            var originalAmount = UnitConversion.ToBigInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(symbol, sender.Address);
            Assert.IsTrue(balance == originalAmount);

            // do a side chain send using test user balance from root to account chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, sender.Address, targetChain, sideAmount, 0);
            var blockA = simulator.EndBlock().FirstOrDefault();
            Assert.IsTrue(blockA != null);

            // finish the chain transfer from parent to child
            simulator.BeginBlock();
            var txB = simulator.GenerateSideChainSettlement(sender, sourceChain, targetChain, blockA.Hash);
            Assert.IsTrue(simulator.EndBlock().Any());

            // verify balances
            var feeB = targetChain.GetTransactionFee(txB);
            balance = targetChain.GetTokenBalance(symbol, sender.Address);
            Assert.IsTrue(balance == sideAmount - feeB);

            var feeA = sourceChain.GetTransactionFee(txA);
            var leftoverAmount = originalAmount - (sideAmount + feeA);

            balance = sourceChain.GetTokenBalance(symbol, sender.Address);
            Assert.IsTrue(balance == leftoverAmount);

            sideAmount /= 2;
            simulator.BeginBlock();
            var txC = simulator.GenerateSideChainSend(sender, symbol, targetChain, sender.Address, sourceChain, sideAmount, 0);
            var blockC = simulator.EndBlock().FirstOrDefault();
            Assert.IsTrue(blockC != null);

            // finish the chain transfer from child to parent
            simulator.BeginBlock();
            var txD = simulator.GenerateSideChainSettlement(sender, targetChain, sourceChain, blockC.Hash);
            Assert.IsTrue(simulator.EndBlock().Any());
        }

        [TestMethod]
        public void SideChainTransferMultipleSteps()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var sideChain = nexus.FindChainByName("bank");

            var symbol = Nexus.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(symbol);

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            var originalAmount = UnitConversion.ToBigInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            var newChainName = "testing";

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.GenerateChain(owner, sideChain, newChainName);
            simulator.EndBlock();

            var targetChain = nexus.FindChainByName(newChainName);

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(symbol, sender.Address);
            Assert.IsTrue(balance == originalAmount);

            // do a side chain send using test user balance from root to apps chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, sender.Address, sideChain, sideAmount, 0);
            var blockA = simulator.EndBlock().FirstOrDefault();
            var evtsA = blockA.GetEventsForTransaction(txA.Hash);

            // finish the chain transfer
            simulator.BeginBlock();
            var txB = simulator.GenerateSideChainSettlement(sender, nexus.RootChain, sideChain, blockA.Hash);
            Assert.IsTrue(simulator.EndBlock().Any());

            // we cant transfer the full side amount due to fees
            // TODO  calculate the proper fee values instead of this
            var txCostA = simulator.Nexus.RootChain.GetTransactionFee(txA);
            var txCostB = sideChain.GetTransactionFee(txB);
            sideAmount = sideAmount - txCostB;

            balance = sideChain.GetTokenBalance(symbol, sender.Address);
            Assert.IsTrue(balance == sideAmount);

            var extraFree = UnitConversion.ToBigInteger(0.01m, token.Decimals);

            sideAmount -= extraFree*10;

            // do another side chain send using test user balance from apps to target chain
            simulator.BeginBlock();
            var txC = simulator.GenerateSideChainSend(sender, symbol, sideChain, receiver.Address, targetChain, sideAmount, extraFree);
            var blockC = simulator.EndBlock().FirstOrDefault();

            var evtsC = blockC.GetEventsForTransaction(txC.Hash);

            var appSupplies = new SupplySheet(symbol, sideChain, nexus);
            var childBalance = appSupplies.GetChildBalance(sideChain.Storage, targetChain.Name);
            var expectedChildBalance = sideAmount + extraFree;
            
            // finish the chain transfer
            simulator.BeginBlock();
            var txD = simulator.GenerateSideChainSettlement(receiver, sideChain, targetChain, blockC.Hash);
            Assert.IsTrue(simulator.EndBlock().Any());

            // TODO  verify balances
        }

        [TestMethod]
        public void NftMint()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = KeyPair.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, Blockchain.Tokens.TokenFlags.None);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(symbol);
            Assert.IsTrue(nexus.TokenExists(symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken directly on the user
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

            //verify that the present nft is the same we actually tried to create
            var tokenId = ownedTokenList.ElementAt(0);
            var nft = nexus.GetNFT(symbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) && nft.RAM.SequenceEqual(tokenRAM),
                "And why is this NFT different than expected? Not the same data");

            var currentSupply = nexus.GetTokenSupply(chain.Storage, symbol);
            Assert.IsTrue(currentSupply == 1, "why supply did not increase?");
        }


        [TestMethod]
        public void NftBurn()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = KeyPair.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Burnable);
            simulator.EndBlock();

            // Send some SOUL to the test user (required for gas used in "burn" transaction)
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, chain, Nexus.FuelTokenSymbol, UnitConversion.ToBigInteger(1, Nexus.FuelTokenDecimals));
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(symbol);
            var tokenData = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            Assert.IsTrue(nexus.TokenExists(symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the user already have a CoolToken?");

            // Mint a new CoolToken directly on the user
            simulator.BeginBlock();
            BigInteger tokenKCALWorth = 100;
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenData, new byte[0], tokenKCALWorth);
            simulator.EndBlock();

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the user not have one now?");

            var ownerAddress = ownerships.GetOwner(chain.Storage, 1);
            Assert.IsTrue(ownerAddress == testUser.Address);

            //verify that the present nft is the same we actually tried to create
            var tokenId = ownedTokenList.ElementAt(0);
            var nft = nexus.GetNFT(symbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenData) || nft.RAM.SequenceEqual(tokenData),
                "And why is this NFT different than expected? Not the same data");

            // burn the token
            simulator.BeginBlock();
            simulator.GenerateNftBurn(testUser, chain, symbol, tokenId);
            simulator.EndBlock();

            //verify the user no longer has the token
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the user still have it post-burn?");
        }

        [TestMethod]
        public void NftTransfer()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var nftKey = KeyPair.Generate();
            var nftSymbol = "COOL";
            var nftName = "CoolToken";

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            // Send some SOUL to the test user (required for gas used in "transfer" transaction)
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, chain, Nexus.FuelTokenSymbol, UnitConversion.ToBigInteger(1, Nexus.FuelTokenDecimals));
            simulator.EndBlock();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, nftSymbol, nftName, 0, 0, TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nftSymbol);
            var tokenData = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            Assert.IsTrue(nexus.TokenExists(nftSymbol), "Can't find the token symbol");

            // verify nft presence on the sender pre-mint
            var ownerships = new OwnershipSheet(nftSymbol);
            var ownedTokenList = ownerships.Get(chain.Storage, sender.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            // Mint a new CoolToken directly on the sender
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, sender.Address, nftSymbol, tokenData, new byte[0], 0);
            simulator.EndBlock();

            // verify nft presence on the sender post-mint
            ownedTokenList = ownerships.Get(chain.Storage, sender.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

            //verify that the present nft is the same we actually tried to create
            var tokenId = ownedTokenList.ElementAt(0);
            var nft = nexus.GetNFT(nftSymbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenData) || nft.RAM.SequenceEqual(tokenData),
                "And why is this NFT different than expected? Not the same data");

            // verify nft presence on the receiver pre-transfer
            ownedTokenList = ownerships.Get(chain.Storage, receiver.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the receiver already have a CoolToken?");

            // transfer that nft from sender to receiver
            simulator.BeginBlock();
            var txA = simulator.GenerateNftTransfer(sender, receiver.Address, chain, nftSymbol, tokenId);
            simulator.EndBlock();

            // verify nft presence on the receiver post-transfer
            ownedTokenList = ownerships.Get(chain.Storage, receiver.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the receiver not have one now?");

            //verify that the transfered nft is the same we actually tried to create
            tokenId = ownedTokenList.ElementAt(0);
            nft = nexus.GetNFT(nftSymbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenData) || nft.RAM.SequenceEqual(tokenData),
                "And why is this NFT different than expected? Not the same data");
        }

        [TestMethod]
        public void SidechainNftTransfer()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.FindChainByName("privacy");

            var nftSymbol = "COOL";

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            var fullAmount = UnitConversion.ToBigInteger(10, Nexus.FuelTokenDecimals);
            var smallAmount = fullAmount / 2;
            Assert.IsTrue(smallAmount > 0);

            // Send some SOUL to the test user (required for gas used in "transfer" transaction)
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, sourceChain, Nexus.FuelTokenSymbol, fullAmount);
            simulator.EndBlock();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, nftSymbol, "CoolToken", 0, 0, TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nftSymbol);
            var tokenData = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            Assert.IsTrue(nexus.TokenExists(nftSymbol), "Can't find the token symbol");

            // verify nft presence on the sender pre-mint
            var ownerships = new OwnershipSheet(nftSymbol);
            var ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            // Mint a new CoolToken directly on the sender
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, sender.Address, nftSymbol, tokenData, new byte[0], 0);
            simulator.EndBlock();

            // verify nft presence on the sender post-mint
            ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

            //verify that the present nft is the same we actually tried to create
            var tokenId = ownedTokenList.ElementAt(0);
            var nft = nexus.GetNFT(nftSymbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenData) || nft.RAM.SequenceEqual(tokenData),
                "And why is this NFT different than expected? Not the same data");

            // verify nft presence on the receiver pre-transfer
            ownedTokenList = ownerships.Get(targetChain.Storage, receiver.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the receiver already have a CoolToken?");

            var extraFee = UnitConversion.ToBigInteger(0.001m, Nexus.FuelTokenDecimals);

            // transfer that nft from sender to receiver
            simulator.BeginBlock();
            simulator.GenerateSideChainSend(sender, Nexus.FuelTokenSymbol, sourceChain, receiver.Address, targetChain, smallAmount, extraFee);
            var txA = simulator.GenerateNftSidechainTransfer(sender, receiver.Address, sourceChain, targetChain, nftSymbol, tokenId);
            simulator.EndBlock();

            var blockA = nexus.RootChain.LastBlock;

            // finish the chain transfer
            simulator.BeginBlock();
            simulator.GenerateSideChainSettlement(receiver, nexus.RootChain, targetChain, blockA.Hash);
            Assert.IsTrue(simulator.EndBlock().Any());

            // verify the sender no longer has it
            ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender still have one?");

            // verify nft presence on the receiver post-transfer
            ownedTokenList = ownerships.Get(targetChain.Storage, receiver.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the receiver not have one now?");

            //verify that the transfered nft is the same we actually tried to create
            tokenId = ownedTokenList.ElementAt(0);
            nft = nexus.GetNFT(nftSymbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenData) || nft.RAM.SequenceEqual(tokenData),
                "And why is this NFT different than expected? Not the same data");
        }

        [TestMethod]
        public void TestNoGasSameChainTransfer()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var accountChain = nexus.FindChainByName("account");

            var symbol = Nexus.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(symbol);

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            var amount = UnitConversion.ToBigInteger(1, token.Decimals);

            var oldBalance = nexus.RootChain.GetTokenBalance(symbol, owner.Address);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(symbol, sender.Address);
            Assert.IsTrue(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(symbol, owner.Address);
            var gasFee = nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(transferBalance + newBalance + gasFee == oldBalance);

            //Try to send the entire balance without affording fees from sender to receiver
            try
            {
                simulator.BeginBlock();
                tx = simulator.GenerateTransfer(sender, receiver.Address, nexus.RootChain, symbol, transferBalance);
                simulator.EndBlock();
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
            }

            // verify balances, receiver should have 0 balance
            transferBalance = nexus.RootChain.GetTokenBalance(symbol, receiver.Address);
            Assert.IsTrue(transferBalance == 0, "Transaction failed completely as expected");
        }

        [TestMethod]
        public void NoGasTestSideChainTransfer()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.FindChainByName("privacy");

            var symbol = Nexus.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(symbol);

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            var originalAmount = UnitConversion.ToBigInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(symbol, sender.Address);
            Assert.IsTrue(balance == originalAmount);

            Transaction txA = null, txB = null;

            try
            {
                // do a side chain send using test user balance from root to account chain
                simulator.BeginBlock();
                txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, receiver.Address, targetChain,
                    originalAmount, 1);
                simulator.EndBlock();
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
            }

            try
            {
                var blockA = nexus.RootChain.LastBlock;

                // finish the chain transfer
                simulator.BeginBlock();
                txB = simulator.GenerateSideChainSettlement(sender, nexus.RootChain, targetChain, blockA.Hash);
                Assert.IsTrue(simulator.EndBlock().Any());
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
            }


            // verify balances, receiver should have 0 balance
            balance = targetChain.GetTokenBalance(symbol, receiver.Address);
            Assert.IsTrue(balance == 0);
        }


        [TestMethod]
        public void TestAddressComparison()
        {
            var owner = KeyPair.FromWIF("L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25");
            var address = Address.FromText("P2f7ZFuj6NfZ76ymNMnG3xRBT5hAMicDrQRHE4S7SoxEr");

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            Assert.IsTrue(address.Text == nexus.GenesisAddress.Text);
            Assert.IsTrue(address.PublicKey.SequenceEqual(nexus.GenesisAddress.PublicKey));
            Assert.IsTrue(address == nexus.GenesisAddress);
        }

    }
}
