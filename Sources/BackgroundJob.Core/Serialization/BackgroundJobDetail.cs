using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using BackgroundJob.Core.Serialization;

namespace BackgroundJob.Host
{
    internal class BackgroundJobDetail:IBackgroundJobDetail
    {
        public BackgroundJobDetail()
        {
        }

        public BackgroundJobDetail(Type type, MethodInfo method,  string[] arguments)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            if (method == null)
                throw new ArgumentNullException("method");
            if (arguments == null)
                throw new ArgumentNullException("arguments");
            Type = type;
            Method = method;
            Arguments = arguments;
            Validate();
        }

        public string[] Arguments { get; set; }

        public MethodInfo Method { get; set; }

        public Type Type { get; set; }

        public static BackgroundJobDetail FromExpression(Expression<Action> methodCall)
        {
            if (methodCall == null)
                throw new ArgumentNullException("methodCall");
            var callExpression = methodCall.Body as MethodCallExpression;
            if (callExpression == null)
                throw new NotSupportedException("Expression body should be of type `MethodCallExpression`");
            return new BackgroundJobDetail(callExpression.Method.DeclaringType, callExpression.Method, GetArguments(callExpression));
        }

        public static BackgroundJobDetail FromExpression<T>(Expression<Action<T>> methodCall)
        {
            if (methodCall == null)
                throw new ArgumentNullException("methodCall");
            var callExpression = methodCall.Body as MethodCallExpression;
            if (callExpression == null)
                throw new NotSupportedException("Expression body should be of type `MethodCallExpression`");
            return new BackgroundJobDetail(typeof (T), callExpression.Method, GetArguments(callExpression));
        }

        private static string[] GetArguments(MethodCallExpression callExpression)
        {
            var objArray = callExpression.Arguments.Select(GetArgumentValue).ToArray();
            var list = new List<string>(objArray.Length);
            foreach (var obj in objArray)
            {
                string str = null;
                if (obj != null&&!(obj is CancellationToken))
                    str = !(obj is DateTime)
                        ? JobHelper.ToJson(obj)
                        : ((DateTime) obj).ToString("o", CultureInfo.InvariantCulture);
                list.Add(str);
            }
            return list.ToArray();
        }

        private static object GetArgumentValue(Expression expression)
        {
            var constantExpression = expression as ConstantExpression;
            return constantExpression != null ? constantExpression.Value : Expression.Lambda(expression).Compile().DynamicInvoke();
        }

        private object[] DeserializeArguments(CancellationToken cancellationToken)
        {
            try
            {
                var parameters = Method.GetParameters();
                var list = new List<object>(Arguments.Length);
                for (var index = 0; index < parameters.Length; ++index)
                {
                    var parameterInfo = parameters[index];
                    var text = Arguments[index];
                    object obj;
                    if (typeof(CancellationToken).IsAssignableFrom(parameterInfo.ParameterType))
                    {
                        obj = cancellationToken;
                    }
                    else
                    {
                        try
                        {
                            obj = text != null ? JobHelper.FromJson(text, parameterInfo.ParameterType) : null;
                        }
                        catch
                        {
                            obj = !(parameterInfo.ParameterType == typeof(object)) ? TypeDescriptor.GetConverter(parameterInfo.ParameterType).ConvertFromInvariantString(text) : text;
                        }
                    }
                    list.Add(obj);
                }
                return list.ToArray();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("An exception occurred during arguments deserialization.", ex);
            }
        }

        public void Perform(IJobActivator activator, CancellationToken cancellationToken)
        {
            if (activator == null)
                throw new ArgumentNullException("activator");
            if (cancellationToken == null)
                throw new ArgumentNullException("cancellationToken");
            var deserializedArguments = DeserializeArguments(cancellationToken);
            if (!Method.IsStatic)
                Activate(activator, deserializedArguments);
            else
                InvokeMethod(null, deserializedArguments);
        }

        private void Activate(IJobActivator activator, object[] deserializedArguments)
        {
            activator.ActivateJob(Type, Method, deserializedArguments);
        }

        private object InvokeMethod(object instance, object[] deserializedArguments)
        {
            try
            {
                return Method.Invoke(instance, deserializedArguments);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException is OperationCanceledException)
                    throw ex.InnerException;
                throw new JobFailedException("При выполнении задачи произошла ошибка", ex.InnerException);
            }
        }

        private static void Dispose(object instance)
        {
            try
            {
                var disposable = instance as IDisposable;
                if (disposable == null)
                    return;
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Job has been performed, but an exception occurred during disposal.", ex);
            }
        }

        private void Validate()
        {
            if (Method.DeclaringType == null)
                throw new NotSupportedException("Global methods are not supported. Use class methods instead.");
            if (!Method.DeclaringType.IsAssignableFrom(Type))
                throw new ArgumentException(string.Format("The type `{0}` must be derived from the `{1}` type.", Method.DeclaringType, Type));
            if (!Method.IsPublic)
                throw new NotSupportedException("Only public methods can be invoked in the background.");
            var parameters = Method.GetParameters();
            if (parameters.Length != Arguments.Length)
                throw new ArgumentException("Argument count must be equal to method parameter count.");
            foreach (var parameterInfo in parameters)
            {
                if (parameterInfo.IsOut)
                    throw new NotSupportedException("Output parameters are not supported: there is no guarantee that specified method will be invoked inside the same process.");
                if (parameterInfo.ParameterType.IsByRef)
                    throw new NotSupportedException("Parameters, passed by reference, are not supported: there is no guarantee that specified method will be invoked inside the same process.");
            }
        }
    }

    public interface IBackgroundJobDetail
    {
        void Perform(IJobActivator activator, CancellationToken cancellationToken);
    }
}