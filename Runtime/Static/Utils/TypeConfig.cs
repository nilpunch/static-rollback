using System;
using System.Reflection;
using FFS.Libraries.StaticPack;

namespace Shenanicode.Rollback {
	public struct StaticTypeConfig<T> where T : struct {
		public static readonly IPackArrayStrategy<T> ReadWriteStrategy;

		public static readonly T DefaultValue;

		static StaticTypeConfig() {
			var configType = typeof(TypeConfig<>).MakeGenericType(typeof(T));
			var staticConfig = (TypeConfig<T>)(TypeConfig.FindStaticConfig(typeof(T), configType, "Config")
												?? Activator.CreateInstance(configType));
			var finalConfig = staticConfig.MergeWith(TypeConfig<T>.Default);

			ReadWriteStrategy = finalConfig.ReadWriteStrategy;
			DefaultValue = finalConfig.DefaultValue!.Value;
		}
	}

	public struct TypeConfig<T> where T : struct {
		public IPackArrayStrategy<T> ReadWriteStrategy;

		public T? DefaultValue;

		public static readonly TypeConfig<T> Default = new() {
			DefaultValue = default(T),
			ReadWriteStrategy = TypeUtils.TryCreateUnmanagedPackArrayStrategy<T>() ?? new StructPackArrayStrategy<T>(),
		};

		internal TypeConfig<T> MergeWith(TypeConfig<T> other) {
			return new TypeConfig<T> {
				DefaultValue = DefaultValue ?? other.DefaultValue,
				ReadWriteStrategy = ReadWriteStrategy ?? other.ReadWriteStrategy,
			};
		}
	}

	public static class TypeConfig {
		public static object FindStaticConfig(Type type, Type configType, string preferredName) {
			object result = null;
			var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
			foreach (var field in fields) {
				if (field.FieldType == configType) {
					if (field.Name == preferredName) {
						return field.GetValue(null);
					}
					result ??= field.GetValue(null);
				}
			}
			if (result != null) {
				return result;
			}

			var properties = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
			foreach (var property in properties) {
				if (property.PropertyType == configType) {
					if (property.Name == preferredName) {
						return property.GetValue(null);
					}
					result ??= property.GetValue(null);
				}
			}
			return result;
		}
	}
}
