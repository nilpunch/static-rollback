using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FFS.Libraries.StaticPack;

namespace Shenanicode.Rollback {
	public static class TypeUtils {
		/// <summary>
		/// Returns full type name with namespace and generic arguments.
		/// </summary>
		public static string GetFullGenericName(this Type type) {
			if (type.IsGenericType) {
				var genericArguments = string.Join(',', type.GetGenericArguments().Select(GetFullGenericName));
				var typeItself = type.FullName[..type.FullName.IndexOf('`', StringComparison.Ordinal)];
				return $"{typeItself}<{genericArguments}>";
			}
			return type.FullName;
		}

		/// <summary>
		/// Returns type name with generic arguments.
		/// </summary>
		public static string GetGenericName(this Type type) {
			if (type.IsGenericType) {
				var genericArguments = string.Join(',', type.GetGenericArguments().Select(GetGenericName));
				var typeItself = type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)];
				return $"{typeItself}<{genericArguments}>";
			}
			return type.Name;
		}

		public static bool HasNoFields(Type type) {
			return !HasAnyFields(type);
		}

		public static bool HasAnyFields(Type type) {
			return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length > 0;
		}

		private static readonly Dictionary<Type, bool> s_managedCache = new();

		public static bool IsManaged(this Type type) {
			return !IsUnmanaged(type);
		}

		public static bool IsUnmanaged(this Type type) {
			if (!s_managedCache.TryGetValue(type, out var isUnmanaged)) {
				if (type.IsPrimitive || type.IsPointer || type.IsEnum) {
					isUnmanaged = true;
				}
				else if (!type.IsValueType) {
					isUnmanaged = false;
				}
				else {
					isUnmanaged = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
									.All(x => x.FieldType.IsUnmanaged());
				}
				s_managedCache.Add(type, isUnmanaged);
			}

			return isUnmanaged;
		}

		private static readonly Dictionary<Type, int> s_sizeOfCache = new();

		public static unsafe uint SizeOf<T>() where T : unmanaged => (uint)sizeof(T);

		public static int SizeOfUnmanaged(Type t) {
			if (!s_sizeOfCache.TryGetValue(t, out var size)) {
				try {
					size = SizeOfGeneric(t);
				}
				catch {
					throw new Exception($"Can't get runtime size of {t.GetFullGenericName()}.");
				}
				s_sizeOfCache.Add(t, size);
			}

			return size;
		}

		private static int SizeOfGeneric(Type t) {
			var genericMethod = typeof(TypeUtils)
								.GetMethod(nameof(SizeOf), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
								!.MakeGenericMethod(t);
			var size = (int)genericMethod.Invoke(null, new object[] { });
			return size;
		}

		internal static IPackArrayStrategy<T> TryCreateUnmanagedPackArrayStrategy<T>() where T : struct {
			try {
				var unmanagedPackStrategyType = typeof(UnmanagedPackArrayStrategy<>).MakeGenericType(typeof(T));
				return (IPackArrayStrategy<T>)Activator.CreateInstance(unmanagedPackStrategyType);
			}
			catch (ArgumentException argumentException) {
				return null;
			}

			return null;
		}

		internal static bool TryRegisterUnmanagedPacking<T>() where T : struct {
			if (BinaryPack.IsRegistered<T>()) {
				return true;
			}

			try {
				var register = typeof(TypeUtils)
								.GetMethod(nameof(RegisterUnmanagedPacking), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
								!.MakeGenericMethod(typeof(T));
				register.Invoke(null, new object[] { });
				return true;
			}
			catch (Exception) {
				return false;
			}
		}

		internal static void RegisterUnmanagedPacking<T>() where T : unmanaged {
			BinaryPack.Register(UnmanagedWrite, UnmanagedRead<T>);
		}

		internal static unsafe void UnmanagedWrite<T>(ref BinaryPackWriter writer, in T value) where T : unmanaged {
			var size = (uint)sizeof(T);
			writer.EnsureSize(size);
			fixed (byte* dest = &writer.Buffer[writer.Position])
			fixed (T* src = &value) {
				Buffer.MemoryCopy(src, dest, size, size);
			}
			writer.Position += size;
		}

		internal static unsafe T UnmanagedRead<T>(ref BinaryPackReader reader) where T : unmanaged {
			var value = default(T);
			var size = (uint)sizeof(T);
			fixed (byte* src = &reader.Buffer[reader.Position])
				Buffer.MemoryCopy(src, &value, size, size);
			reader.Position += size;
			return value;
		}
	}
}
