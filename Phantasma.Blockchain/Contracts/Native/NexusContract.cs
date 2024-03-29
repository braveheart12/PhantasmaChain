﻿using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct TokenEventData
    {
        public string symbol;
        public BigInteger value;
        public Address chainAddress;
    }

    public struct TokenMetadata
    {
        public string key;
        public byte[] value;
    }

    public struct MetadataEventData
    {
        public string symbol;
        public TokenMetadata metadata;
    }

    public sealed class NexusContract : SmartContract
    {
        public override string Name => "nexus";

        public const int MAX_TOKEN_DECIMALS = 18;

        private StorageMap _tokenMetadata;

        public NexusContract() : base()
        {
        }

        public void CreateToken(Address owner, string symbol, string name, BigInteger maxSupply, BigInteger decimals, TokenFlags flags, byte[] script)
        {
            Runtime.Expect(!string.IsNullOrEmpty(symbol), "token symbol required");
            Runtime.Expect(!string.IsNullOrEmpty(name), "token name required");
            Runtime.Expect(maxSupply >= 0, "token supply cant be negative");
            Runtime.Expect(decimals >= 0, "token decimals cant be negative");
            Runtime.Expect(decimals <= MAX_TOKEN_DECIMALS, $"token decimals cant exceed {MAX_TOKEN_DECIMALS}");

            if (symbol == Nexus.FuelTokenSymbol)
            {
                Runtime.Expect(flags.HasFlag(TokenFlags.Fuel), "token should be native");
            }
            else
            {
                Runtime.Expect(!flags.HasFlag(TokenFlags.Fuel), "token can't be native");
            }

            if (symbol == Nexus.StakingTokenSymbol)
            {
                Runtime.Expect(flags.HasFlag(TokenFlags.Stakable), "token should be stakable");
            }

            if (symbol == Nexus.StableTokenSymbol)
            {
                Runtime.Expect(flags.HasFlag(TokenFlags.Stable), "token should be stable");
            }

            if (flags.HasFlag(TokenFlags.External))
            {
                Runtime.Expect(owner == Runtime.Nexus.GenesisAddress, "external token not permitted");
            }

            Runtime.Expect(IsWitness(owner), "invalid witness");

            symbol = symbol.ToUpperInvariant();

            Runtime.Expect(this.Runtime.Nexus.CreateToken(owner, symbol, name, maxSupply, (int)decimals, flags, script), "token creation failed");
            Runtime.Notify(EventKind.TokenCreate, owner, symbol);
        }

        public void CreateChain(Address owner, string name, string parentName, string[] contracts)
        {
            Runtime.Expect(!string.IsNullOrEmpty(name), "name required");
            Runtime.Expect(!string.IsNullOrEmpty(parentName), "parent chain required");

            Runtime.Expect(IsWitness(owner), "invalid witness");

            name = name.ToLowerInvariant();
            Runtime.Expect(!name.Equals(parentName, StringComparison.OrdinalIgnoreCase), "same name as parent");

            var parent = this.Runtime.Nexus.FindChainByName(parentName);
            Runtime.Expect(parent != null, "invalid parent");

            var chain = this.Runtime.Nexus.CreateChain(this.Storage, owner, name, parent, this.Runtime.Block, contracts);
            Runtime.Expect(chain != null, "chain creation failed");

            Runtime.Notify(EventKind.ChainCreate, owner, chain.Address);
        }

        public void SetTokenMetadata(string symbol, string key, byte[] value)
        {
            Runtime.Expect(Runtime.Nexus.TokenExists(symbol), "token not found");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);

            Runtime.Expect(IsWitness(tokenInfo.Owner), "invalid witness");

            var metadataEntries = _tokenMetadata.Get<string, StorageList>(symbol);

            int index = -1;

            var count = metadataEntries.Count();
            for (int i = 0; i < count; i++)
            {
                var temp = metadataEntries.Get<TokenMetadata>(i);
                if (temp.key == key)
                {
                    index = i;
                    break;
                }
            }

            var metadata = new TokenMetadata() { key = key, value = value };
            if (index >= 0)
            {
                metadataEntries.Replace<TokenMetadata>(index, metadata);
            }
            else
            {
                metadataEntries.Add<TokenMetadata>(metadata);
            }

            Runtime.Notify(EventKind.Metadata, tokenInfo.Owner, new MetadataEventData() { symbol = symbol, metadata = metadata });
        }

        public byte[] GetTokenMetadata(string symbol, string key)
        {
            Runtime.Expect(Runtime.Nexus.TokenExists(symbol), "token not found");
            var token = this.Runtime.Nexus.GetTokenInfo(symbol);

            var metadataEntries = _tokenMetadata.Get<string, StorageList>(symbol);

            var count = metadataEntries.Count();
            for (int i = 0; i < count; i++)
            {
                var temp = metadataEntries.Get<TokenMetadata>(i);
                if (temp.key == key)
                {
                    return temp.value;
                }
            }

            return null;
        }

        public TokenMetadata[] GetTokenMetadataList(string symbol)
        {
            Runtime.Expect(Runtime.Nexus.TokenExists(symbol), "token not found");
            var token = this.Runtime.Nexus.GetTokenInfo(symbol);

            var metadataEntries = _tokenMetadata.Get<string, StorageList>(symbol);

            return metadataEntries.All<TokenMetadata>();
        }

    }
}
