﻿using System;
using System.Collections.Generic;
using Alex.API.Blocks;
using Alex.API.Blocks.State;
using Alex.API.Graphics;
using Alex.API.Utils;
using Alex.API.World;
using Alex.Blocks.Minecraft;
using Alex.Networking.Java.Util;
using Alex.ResourcePackLib.Json;
using Alex.Utils;
using Alex.Worlds;
using Alex.Worlds.Lighting;
using Microsoft.Xna.Framework;
using NLog;
using BitArray = Alex.API.Utils.BitArray;

namespace Alex.Blocks.Storage
{
    public class ChunkSection : IChunkSection
    {
		private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(ChunkSection));

		private int _yBase;
		private int _blockRefCount;
		private int _tickRefCount;
		
		//private BlockStorage Data;
		private BlockStorage[] _blockStorages;
		public NibbleArray BlockLight;
		public NibbleArray SkyLight;

        public System.Collections.BitArray TransparentBlocks;
        public System.Collections.BitArray SolidBlocks;
        public System.Collections.BitArray RenderedBlocks;
        
        private System.Collections.BitArray ScheduledUpdates;
        private System.Collections.BitArray ScheduledSkylightUpdates;
        private System.Collections.BitArray ScheduledBlocklightUpdates;

        public int ScheduledUpdatesCount { get; private set; } = 0;
        public int ScheduledSkyUpdatesCount { get; private set; } = 0;
        public int ScheduledBlockUpdatesCount { get; private set; } = 0;
        
        public List<BlockCoordinates> LightSources { get; } = new List<BlockCoordinates>();

        public bool SolidBorder { get; private set; } = false;
		private bool[] FaceSolidity { get; set; } = new bool[6];
		public bool HasAirPockets { get; private set; } = true;
		public bool IsAllAir => _blockRefCount == 0;

		internal ChunkMesh MeshCache { get; set; } = null;
		internal IReadOnlyDictionary<BlockCoordinates, IList<ChunkMesh.EntryPosition>> MeshPositions { get; set; } = null;
		
		private ChunkColumn Owner { get; }
        public ChunkSection(ChunkColumn owner, int y, bool storeSkylight, int sections = 2)
        {
	        Owner = owner;
	        if (sections <= 0)
		        sections = 1;
	        
	        this._yBase = y;
	        //Data = new BlockStorage();
	        _blockStorages = new BlockStorage[sections];
	        for (int i = 0; i < sections; i++)
	        {
		        _blockStorages[i] = new BlockStorage();
	        }
	        
	        this.BlockLight = new NibbleArray(4096, 0);

			if (storeSkylight)
			{
				this.SkyLight = new NibbleArray(4096, 0xff);
			}
//System.Collections.BitArray a = new System.Collections.BitArray(new byte[(16 * 16 * 16) / 8]);

		    TransparentBlocks = new System.Collections.BitArray(new byte[(16 * 16 * 16) / 8]);
		    SolidBlocks = new System.Collections.BitArray(new byte[(16 * 16 * 16) / 8]);
		    ScheduledUpdates = new System.Collections.BitArray(new byte[(16 * 16 * 16) / 8]);
		    ScheduledSkylightUpdates = new System.Collections.BitArray(new byte[(16 * 16 * 16) / 8]);
		    ScheduledBlocklightUpdates = new System.Collections.BitArray(new byte[(16 * 16 * 16) / 8]);
            RenderedBlocks = new System.Collections.BitArray(new byte[(16 * 16 * 16) / 8]);
		
            for (int i = 0; i < TransparentBlocks.Length; i++)
			{
				TransparentBlocks[i] = true;
				SolidBlocks[i] = false;
			}
        }

        private bool _isDirty = false;
        public bool IsDirty
        {
	        get
	        {
		        return New || ScheduledUpdatesCount > 0 || ScheduledBlockUpdatesCount > 0 ||
		               ScheduledSkyUpdatesCount > 0 || _isDirty;
	        }
	        set
	        {
		        _isDirty = value;
	        }
        }

        public bool New { get; set; } = true;

        public void ResetSkyLight(byte initialValue = 0xff)
        {
	        Owner.SkyLightDirty = true;
			this.SkyLight = new NibbleArray(4096, initialValue);
		}

		private static int GetCoordinateIndex(int x, int y, int z)
		{
			return (y << 8 | z << 4 | x);
		}

        public void SetRendered(int x, int y, int z, bool value)
        {
            RenderedBlocks[GetCoordinateIndex(x, y, z)] = value;
        }

        public bool IsRendered(int x, int y, int z)
        {
            return RenderedBlocks[GetCoordinateIndex(x, y, z)];
        }

        public bool IsScheduled(int x, int y, int z)
		{
		    return ScheduledUpdates.Get(GetCoordinateIndex(x, y, z));
		}

		public void SetScheduled(int x, int y, int z, bool value)
		{
			var idx = GetCoordinateIndex(x, y, z);

			var oldValue = ScheduledUpdates[idx];

			if (oldValue && !value)
			{
				ScheduledUpdatesCount--;
			}
			else if (!oldValue && value)
			{
				ScheduledUpdatesCount++;
			}
			
            ScheduledUpdates.Set(idx, value);
		}

		public bool IsBlockLightScheduled(int x, int y, int z)
		{
			return ScheduledBlocklightUpdates.Get(GetCoordinateIndex(x, y, z));
		}

		public void SetBlockLightScheduled(int x, int y, int z, bool value)
		{
			var idx = GetCoordinateIndex(x, y, z);

			var oldValue = ScheduledBlocklightUpdates[idx];

			if (oldValue && !value)
			{
				ScheduledBlockUpdatesCount--;
			}
			else if (!oldValue && value)
			{
				ScheduledBlockUpdatesCount++;
			}
			
			ScheduledBlocklightUpdates.Set(idx, value);
		}

		public bool IsLightingScheduled(int x, int y, int z)
		{
		    return
		        ScheduledSkylightUpdates.Get(GetCoordinateIndex(x, y,
		            z));
		}

		public bool SetLightingScheduled(int x, int y, int z, bool value)
		{
			var idx = GetCoordinateIndex(x, y, z);

			var oldValue = ScheduledSkylightUpdates[idx];

			if (oldValue && !value)
			{
				ScheduledSkyUpdatesCount--;
			}
			else if (!oldValue && value)
			{
				ScheduledSkyUpdatesCount++;
			}
			
			ScheduledSkylightUpdates.Set(idx, value);

			return value;
		}

        public IBlockState Get(int x, int y, int z)
		{
			return this.Get(x, y, z, 0);
		}

        public IEnumerable<(IBlockState state, int storage)> GetAll(int x, int y, int z)
        {
	        for (int i = 0; i < _blockStorages.Length; i++)
	        {
		        yield return (Get(x, y, z, i), i);
	        }
        }

        public IBlockState Get(int x, int y, int z, int section)
        {
	        if (section > _blockStorages.Length)
		        throw new IndexOutOfRangeException($"The storage id {section} does not exist!");

	        return _blockStorages[section].Get(x, y, z);
        }

        public void Set(int x, int y, int z, IBlockState state)
        {
	        Set(0, x, y, z, state);
        }

		public void Set(int storage, int x, int y, int z, IBlockState state)
		{
			if (storage > _blockStorages.Length)
				throw new IndexOutOfRangeException($"The storage id {storage} does not exist!");
			
			var blockCoordinates = new BlockCoordinates(x, y, z);
			
			if (state == null)
			{
				Log.Warn($"State == null");
				return;
			}

			var coordsIndex = GetCoordinateIndex(x, y, z);

			if (storage == 0)
			{
				if (state.Block.LightValue > 0)
				{
					if (!LightSources.Contains(blockCoordinates))
					{
						LightSources.Add(blockCoordinates);
					}

					SetBlocklight(x,y,z, (byte) state.Block.LightValue);
					SetBlockLightScheduled(x,y,z, true);
				}
				else
				{
					if (LightSources.Contains(blockCoordinates))
						LightSources.Remove(blockCoordinates);
				}
				
				IBlockState iblockstate = this.Get(x, y, z, storage);
				if (iblockstate != null)
				{
					IBlock block = iblockstate.Block;

					if (!(block is Air))
					{
						--this._blockRefCount;

						if (block.RandomTicked)
						{
							--this._tickRefCount;
						}


						TransparentBlocks.Set(coordsIndex, true);
						SolidBlocks.Set(coordsIndex, false);
					}
				}
			}

			IBlock block1 = state.Block;
            if (storage == 0)
            {
	            if (!(block1 is Air))
	            {
		            ++this._blockRefCount;

		            if (block1.RandomTicked)
		            {
			            ++this._tickRefCount;
		            }

		            TransparentBlocks.Set(coordsIndex, block1.Transparent);
		            SolidBlocks.Set(coordsIndex, block1.Solid);
	            }
            }

            _blockStorages[storage].Set(x, y, z, state);

            //ScheduledUpdates.Set(coordsIndex, true);
            SetScheduled(x,y,z, true);
            
            IsDirty = true;
			
			if (storage == 0 && !block1.Solid)
			{
				HasAirPockets = true;
			}
		}

		public bool IsTransparent(int x, int y, int z)
		{
			return TransparentBlocks.Get(GetCoordinateIndex(x, y, z));
		}

		public bool IsSolid(int x, int y, int z)
		{
		    return SolidBlocks.Get(GetCoordinateIndex(x, y, z));
		}

		public void GetBlockData(int bx, int by, int bz, out bool transparent, out bool solid)
		{
			var coords = GetCoordinateIndex(bx, by, bz);
		    transparent = TransparentBlocks.Get(coords);// TransparentBlocks[coords];
		    solid = SolidBlocks.Get(coords);// SolidBlocks[coords];
		}
		
        public bool IsEmpty()
		{
			return this._blockRefCount == 0;
		}
        
		public bool NeedsRandomTick()
		{
			return this._tickRefCount > 0;
		}

		public int GetYLocation()
		{
			return this._yBase;
		}

		public bool SetSkylight(int x, int y, int z, int value)
		{
			var idx = GetCoordinateIndex(x, y, z);

			var oldSkylight = this.SkyLight[idx];
			if (value != oldSkylight)
			{
				this.SkyLight[idx] = (byte) value;
				SetLightingScheduled(x, y, z, true);
				//ScheduledSkylightUpdates.Set(idx, true);

				Owner.SkyLightDirty = true;

				return true;
			}

			return false;
		}

		public byte GetSkylight(int x, int y, int z)
		{
			return this.SkyLight[GetCoordinateIndex(x,y,z)]; //.get(x, y, z);
		}
		
		public bool SetBlocklight(int x, int y, int z, byte value)
		{
			var idx = GetCoordinateIndex(x, y, z);
			
			var oldBlocklight = this.BlockLight[idx];
			if (oldBlocklight != value)
			{
				this.BlockLight[idx] = value;
				SetBlockLightScheduled(x, y, z, true);
				//ScheduledBlocklightUpdates.Set(idx, true);

				Owner.BlockLightDirty = true;

				return true;
			}

			return false;
		}
		
		public int GetBlocklight(int x, int y, int z)
		{
			return this.BlockLight[GetCoordinateIndex(x,y,z)];
		}

		public void RemoveInvalidBlocks()
		{
			this._blockRefCount = 0;
			this._tickRefCount = 0;

			for (int x = 0; x < 16; x++)
			{
				for (int y = 0; y < 16; y++)
				{
					for (int z = 0; z < 16; z++)
					{
						var idx = GetCoordinateIndex(x, y, z);
						
						//foreach (var state in this.GetAll(x, y, z))
						{
							var block = this.Get(x,y,z, 0).Block;

							TransparentBlocks.Set(idx, block.Transparent);
							SolidBlocks.Set(idx, block.Solid);

							if (!(block is Air))
							{
								++this._blockRefCount;

								if (block.RandomTicked)
								{
									++this._tickRefCount;
								}
							}
						}
					}
				}
			}
			
			CheckForSolidBorder();
		}
		
		private void CheckForSolidBorder()
	    {
	        bool[] solidity = new bool[6]
	        {
	            true,
	            true,
	            true,
	            true,
	            true,
	            true
	        };

	        for (int y = 0; y < 16; y++)
	        {
	            for (int x = 0; x < 16; x++)
	            {
	                if (!SolidBlocks[GetCoordinateIndex(x, y, 0)])
	                {
	                    solidity[2] = false;
	                    SolidBorder = false;
                    }

	                if (!SolidBlocks[GetCoordinateIndex(0, y, x)])
	                {
	                    SolidBorder = false;
	                    solidity[4] = false;
                    }

	                if (!SolidBlocks[GetCoordinateIndex(x, y, 15)])
	                {
	                    SolidBorder = false;
	                    solidity[3] = false;
                    }

	                if (!SolidBlocks[GetCoordinateIndex(15, y, x)])
	                {
	                    SolidBorder = false;
	                    solidity[5] = false;
                    }

	                for (int xx = 0; xx < 16; xx++)
	                {
	                    if (!SolidBlocks[GetCoordinateIndex(xx, 0, x)])
	                    {
	                        SolidBorder = false;
	                        solidity[0] = false;
	                    }

                        if (!SolidBlocks[GetCoordinateIndex(xx, 15, x)])
	                    {
	                        SolidBorder = false;
	                        solidity[1] = false;
	                    }
	                }
	            }
	        }

	        bool airPockets = false;

	        for (int x = 1; x < 15; x++)
	        {
	            for (int y = 1; y < 15; y++)
	            {
	                for (int z = 1; z < 15; z++)
	                {
	                    if (!SolidBlocks[GetCoordinateIndex(x, y, z)])
	                    {
	                        airPockets = true;
	                        break;
	                    }
	                }
                    if (airPockets)
                        break;
	            }

	            if (airPockets)
	                break;
	        }

	        FaceSolidity = solidity;
	        HasAirPockets = airPockets;
	    }

	    public bool IsFaceSolid(BlockFace face)
	    {
	        var intFace = (int) face;

            if (face == BlockFace.None || intFace < 0 || intFace > 5) return false;
	        return FaceSolidity[(int)intFace];
	    }

	    public void Read(MinecraftStream ms)
	    {
		    _blockStorages[0].Read(ms);
	    }
    }
}
