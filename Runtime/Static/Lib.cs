using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Shenanicode.Rollback {
	public class StaticRollbackException : Exception {
		public StaticRollbackException(string message) : base(message) { }

		public StaticRollbackException(string message, Exception innerException) : base(message, innerException) { }
	}

	public static class AutoRegistration {
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void RegisterAll<TSessionType>(params Assembly[] assemblies) where TSessionType : ISessionType {
			var runtimeAssembly = typeof(IInput).Assembly;
			var sessionType = typeof(Session<TSessionType>);
			var tSessionType = sessionType.GetGenericArguments()[0];

			var inputsOpenType = sessionType.GetNestedType("Inputs`1", BindingFlags.Public)
								?? throw new StaticRollbackException($"AutoRegistration: nested type Inputs`1 not found on {sessionType.Name}");
			var signalsOpenType = sessionType.GetNestedType("Signals`1", BindingFlags.Public)
								?? throw new StaticRollbackException($"AutoRegistration: nested type Signals`1 not found on {sessionType.Name}");

			foreach (var assembly in assemblies) {
				if (assembly == runtimeAssembly) {
					continue;
				}

				foreach (var type in assembly.GetTypes().OrderBy(type => type.GetFullGenericName())) {
					if (!type.IsValueType || type.IsAbstract || type.IsGenericTypeDefinition) {
						continue;
					}

					if (typeof(IInput).IsAssignableFrom(type)) {
						GetAutoRegisterGenericMethod(type, inputsOpenType, tSessionType).Invoke(null, null);
					}

					if (typeof(ISignal).IsAssignableFrom(type)) {
						GetAutoRegisterGenericMethod(type, signalsOpenType, tSessionType).Invoke(null, null);
					}
				}
			}
		}

		internal static MethodInfo GetAutoRegisterGenericMethod(Type type, Type openType, Type tSessionType) {
			var genericType = openType.MakeGenericType(tSessionType, type);
			return genericType.GetMethod("AutoRegister", BindingFlags.NonPublic | BindingFlags.Static)
					?? throw new StaticRollbackException($"AutoRegistration: method AutoRegister not found on {genericType.Name}");
		}
	}
}
