using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace BackgroundJob.Host
{
    public class BackgroundJobDetail
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

        public object Perform(IJobActivator activator, CancellationToken cancellationToken)
        {
            if (activator == null)
                throw new ArgumentNullException("activator");
            if (cancellationToken == null)
                throw new ArgumentNullException("cancellationToken");
            var instance = (object)null;
            try
            {
                if (!Method.IsStatic)
                    instance = Activate(activator);
                var deserializedArguments = DeserializeArguments(cancellationToken);
                return InvokeMethod(instance, deserializedArguments);
            }
            finally
            {
                Dispose(instance);
            }
        }

        private object Activate(IJobActivator activator)
        {
            try
            {
                var obj = activator.ActivateJob(Type);
                if (obj == null)
                    throw new InvalidOperationException(string.Format("JobActivator returned NULL instance of the '{0}' type.", Type));
                return obj;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("An exception occurred during job activation.", ex);
            }
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

    public class JobFailedException : Exception
    {
        public JobFailedException(string message, Exception innerException):base(message, innerException)
        {
        }
    }

    public class SerializedJob
    {
        public string Type { get; private set; }

        public string Method { get; private set; }

        public string ParameterTypes { get; private set; }

        public string Arguments { get; set; }

        public SerializedJob(string type, string method, string parameterTypes, string arguments)
        {
            Type = type;
            Method = method;
            ParameterTypes = parameterTypes;
            Arguments = arguments;
        }

        public BackgroundJobDetail Deserialize()
        {
            try
            {
                var type = System.Type.GetType(Type, true, true);
                var types = JobHelper.FromJson<Type[]>(ParameterTypes);
                var method = type.GetMethod(Method, types);
                if (method == null)
                    throw new InvalidOperationException(
                        string.Format("The type `{0}` does not contain a method with signature `{1}({2})`",
                            type.FullName, Method,
                            string.Join(", ",
                                types.Select(x => x.Name))));
                var arguments = JobHelper.FromJson<string[]>(Arguments);
                return new BackgroundJobDetail(type, method, arguments);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Could not load the job. See inner exception for the details.", ex);
            }
        }

        public static SerializedJob Serialize(BackgroundJobDetail job)
        {
            return new SerializedJob(job.Type.AssemblyQualifiedName, job.Method.Name,
                JobHelper.ToJson(
                    job.Method.GetParameters().Select(x => x.ParameterType)),
                JobHelper.ToJson(job.Arguments));
        }
    }
}