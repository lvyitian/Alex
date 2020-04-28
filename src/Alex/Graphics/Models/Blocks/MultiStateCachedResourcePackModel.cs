﻿using System;
using System.Collections.Generic;
using System.Linq;
using Alex.API.Blocks.State;
using Alex.API.Utils;
using Alex.API.World;
using Alex.Blocks.Minecraft;
using Alex.ResourcePackLib.Json.BlockStates;
using Microsoft.Xna.Framework;
using NLog;

namespace Alex.Graphics.Models.Blocks
{
	public class MultiPartModels
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(MultiPartModels));
		public static BlockStateModel[] GetModels(IBlockState blockState, BlockStateResource resource)
		{
			List<BlockStateModel> resultingModels = new List<BlockStateModel>(resource.Parts.Length);

			foreach (var s in resource.Parts)
			{
				if (s.When == null)
				{
					resultingModels.AddRange(s.Apply);
				}
				else if (s.When.Length > 0)
				{
					bool passes = true;
					foreach (var rule in s.When)
					{
						if (!PassesMultiPartRule(rule, blockState))
						{
							passes = false;
							break;
						}
					}

					if (passes)
					{
						resultingModels.AddRange(s.Apply);
					}
				}
			}

			return resultingModels.ToArray();
		}


		public static IBlockState GetBlockState(IWorld world, Vector3 position, IBlockState blockState,
			BlockStateResource blockStateModel)
		{
			var blockStateCopy = blockState;
			foreach (var s in blockStateModel.Parts)
			{
				if (s.When == null)
				{
					//resultingModels.AddRange(s.Apply);
				}
				else if (s.When.Length > 0)
				{
					bool passes = true;
					foreach (var rule in s.When)
					{
						MultiPartRule result;
						if (!PassesMultiPartRule(world, position, rule, blockState, out result))
						{
							passes = false;
							break;
						}
						else
						{
							foreach (var kv in result.KeyValues)
							{
								blockStateCopy = blockStateCopy.WithProperty(kv.Key, kv.Value);
							}
						}
					}

					if (passes)
					{
						//resultingModels.AddRange(s.Apply);
					}
				}
			}

			return blockStateCopy;
		}
		
		public static BlockStateModel[] GetBlockStateModels(IWorld world, Vector3 position, IBlockState blockState, BlockStateResource blockStateModel)
		{
			List<BlockStateModel> resultingModels = new List<BlockStateModel>(blockStateModel.Parts.Length);

			foreach (var s in blockStateModel.Parts)
			{
				if (s.When == null)
				{
					resultingModels.AddRange(s.Apply);
				}
				else if (s.When.Length > 0)
				{
					bool passes = true;
					foreach (var rule in s.When)
					{
						if (!PassesMultiPartRule(world, position, rule, blockState, out _))
						{
							passes = false;
							break;
						}
					}

					if (passes)
					{
						resultingModels.AddRange(s.Apply);
					}
				}
			}

			return resultingModels.ToArray();
		}

		private static bool PassesMultiPartRule(MultiPartRule rule, IBlockState blockState)
		{
			if (rule.HasOrContition)
			{
				return rule.Or.Any(o => PassesMultiPartRule(o, blockState));
			}

			if (rule.HasAndContition)
			{
				return rule.And.All(o => PassesMultiPartRule(o, blockState));
			}

			return rule.KeyValues.All(x => CheckRequirements(blockState, x.Key, x.Value));
			/*
			if (CheckRequirements(blockState, "down", rule.Down)
			    && CheckRequirements(blockState, "up", rule.Up)
			    && CheckRequirements(blockState, "north", rule.North)
			    && CheckRequirements(blockState, "east", rule.East)
			    && CheckRequirements(blockState, "south", rule.South)
			    && CheckRequirements(blockState, "west", rule.West))
			{
				return true;
			}

			return false;*/
		}

		private static bool CheckRequirements(IBlockState baseblockState, string rule, string value)
		{
			if (string.IsNullOrWhiteSpace(value)) return true;

			if (baseblockState.TryGetValue(rule, out string stateValue))
			{
				if (stateValue.Equals(value, StringComparison.InvariantCultureIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		public static bool PassesMultiPartRule(IWorld world, Vector3 position, MultiPartRule rule, IBlockState baseBlock, out MultiPartRule passedRule)
		{
			MultiPartRule s = rule;
			passedRule = rule;
			
			if (rule.HasOrContition)
			{
				if (rule.Or.Any(o =>
				{
					var pass = PassesMultiPartRule(world, position, o, baseBlock, out var p);
					if (pass)
					{
						s = p;
						return true;
					}

					return false;
				}))
				{
					passedRule = s;
					return true;
				};

				return false;
			}

			if (rule.HasAndContition)
			{
				if (rule.And.All(o =>
				{
					var pass = PassesMultiPartRule(world, position, o, baseBlock, out var p);
					if (pass)
					{
						s = p;
						return true;
					}

					return false;
				}))
				{
					passedRule = s;
					return true;
				};

				return false;
			}

			//return rule.All(x => CheckRequirements(baseBlock, x.Key, x.Value));
			return rule.KeyValues.All(x => Passes(world, position, baseBlock, x.Key, x.Value));
			/*
			if (Passes(world, position, baseBlock, "down", rule.Down)
				&& Passes(world, position, baseBlock, "up", rule.Up)
				&& Passes(world, position, baseBlock, "north", rule.North)
				&& Passes(world, position, baseBlock, "east", rule.East)
				&& Passes(world, position, baseBlock, "south", rule.South)
				&& Passes(world, position, baseBlock, "west", rule.West))
			{
				return true;
			}

			return false;*/
		}

		private static bool Passes(IWorld world, Vector3 position, IBlockState baseblockState, string rule,
			string value)
		{
			if (string.IsNullOrWhiteSpace(value)) return true;

			bool checkDirections = true;
			Vector3 direction = Vector3.Zero;
			switch (rule)
			{
				case "north":
					direction = Vector3.Forward;
					break;
				case "east":
					direction = Vector3.Right;
					break;
				case "south":
					direction = Vector3.Backward;
					break;
				case "west":
					direction = Vector3.Left;
					break;
				case "up":
					direction = Vector3.Up;
					break;
				case "down":
					direction = Vector3.Down;
					break;
				default:
					checkDirections = false;
					break;
			}

			bool requiredValue;
			if (value == "true" || value == "false" || value == "none")
			{
				var newPos = new BlockCoordinates(position + direction);
				var blockState = world.GetBlockState(newPos);
				var block = blockState.Block;

				var canAttach = block.Solid && (block.IsFullCube ||
				                                (blockState.Name.Equals(baseblockState.Name,
					                                StringComparison.InvariantCultureIgnoreCase)));
				if (direction == Vector3.Up && !(block is Air))
					return true;

				if (value == "true")
				{
					return canAttach;
				}
				else if (value == "false")
				{
					return !canAttach;
				}
				else if (value == "none")
				{
					return block.BlockMaterial == Material.Air;
				}

				return false;
			}


			if (baseblockState.TryGetValue(rule, out string val))
			{
				return val.Equals(value, StringComparison.InvariantCultureIgnoreCase);
			}

			return false;
		}
	}
}