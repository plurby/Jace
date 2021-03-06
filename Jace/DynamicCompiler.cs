﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Jace.Operations;

namespace Jace
{
    public class DynamicCompiler : IExecutor
    {
        public double Execute(Operation operation)
        {
            return Execute(operation, new Dictionary<string, double>());
        }

        public double Execute(Operation operation, Dictionary<string, int> variables)
        {
            Dictionary<string, double> doubleVariables = new Dictionary<string, double>();
            foreach (string key in variables.Keys)
                doubleVariables.Add(key, variables[key]);

            return Execute(operation, doubleVariables);
        }

        public double Execute(Operation operation, Dictionary<string, double> variables)
        {
            return BuildFunction(operation)(variables);
        }

        public Func<Dictionary<string, double>, double> BuildFunction(Operation operation)
        {
            DynamicMethod method = new DynamicMethod("MyCalcMethod", typeof(double),
                new Type[] { typeof(Dictionary<string, double>) });
            GenerateMethodBody(method, operation);

            Func<Dictionary<string, double>, double> function =
                (Func<Dictionary<string, double>, double>)method.CreateDelegate(typeof(Func<Dictionary<string, double>, double>));

            return function;
        }

        private void GenerateMethodBody(DynamicMethod method, Operation operation)
        {
            ILGenerator generator = method.GetILGenerator();
            generator.DeclareLocal(typeof(double));
            GenerateMethodBody(generator, operation);
            generator.Emit(OpCodes.Ret);
        }

        private void GenerateMethodBody(ILGenerator generator, Operation operation)
        {
            if (operation == null)
                throw new ArgumentNullException("operation");

            if (operation.GetType() == typeof(IntegerConstant))
            {
                IntegerConstant constant = (IntegerConstant)operation;
                
                generator.Emit(OpCodes.Ldc_I4, constant.Value);
                generator.Emit(OpCodes.Conv_R8);
            }
            else if (operation.GetType() == typeof(FloatingPointConstant))
            {
                FloatingPointConstant constant = (FloatingPointConstant)operation;

                generator.Emit(OpCodes.Ldc_R8, constant.Value);
            }
            else if (operation.GetType() == typeof(Variable))
            {
                Type dictionaryType = typeof(Dictionary<string, double>);

                Variable variable = (Variable)operation;

                Label throwExceptionLabel = generator.DefineLabel();
                Label returnLabel = generator.DefineLabel();

                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldstr, variable.Name);
                generator.Emit(OpCodes.Callvirt, dictionaryType.GetMethod("ContainsKey", new Type[] { typeof(string) }));
                generator.Emit(OpCodes.Ldc_I4_0);
                generator.Emit(OpCodes.Ceq);
                generator.Emit(OpCodes.Brtrue_S, throwExceptionLabel);

                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldstr, variable.Name);
                generator.Emit(OpCodes.Callvirt, dictionaryType.GetMethod("get_Item", new Type[] { typeof(string) }));
                generator.Emit(OpCodes.Br_S, returnLabel);

                generator.MarkLabel(throwExceptionLabel);
                generator.Emit(OpCodes.Ldstr, string.Format("The variable \"{0}\" used is not defined.", variable.Name));
                generator.Emit(OpCodes.Newobj, typeof(VariableNotDefinedException).GetConstructor(new Type[] { typeof(string) }));
                generator.Emit(OpCodes.Throw);

                generator.MarkLabel(returnLabel);
            }
            else if (operation.GetType() == typeof(Multiplication))
            {
                Multiplication multiplication = (Multiplication)operation;
                GenerateMethodBody(generator, multiplication.Argument1);
                GenerateMethodBody(generator, multiplication.Argument2);

                generator.Emit(OpCodes.Mul);
            }
            else if (operation.GetType() == typeof(Addition))
            {
                Addition addition = (Addition)operation;
                GenerateMethodBody(generator, addition.Argument1);
                GenerateMethodBody(generator, addition.Argument2);

                generator.Emit(OpCodes.Add);
            }
            else if (operation.GetType() == typeof(Substraction))
            {
                Substraction addition = (Substraction)operation;
                GenerateMethodBody(generator, addition.Argument1);
                GenerateMethodBody(generator, addition.Argument2);

                generator.Emit(OpCodes.Sub);
            }
            else if (operation.GetType() == typeof(Division))
            {
                Division division = (Division)operation;
                GenerateMethodBody(generator, division.Dividend);
                GenerateMethodBody(generator, division.Divisor);

                generator.Emit(OpCodes.Div);
            }
            else if (operation.GetType() == typeof(Exponentiation))
            {
                Exponentiation exponentation = (Exponentiation)operation;
                GenerateMethodBody(generator, exponentation.Base);
                GenerateMethodBody(generator, exponentation.Exponent);

                generator.Emit(OpCodes.Call, typeof(Math).GetMethod("Pow"));
            }
            else if (operation.GetType() == typeof(Function))
            {
                Function function = (Function)operation;

                switch (function.FunctionType)
                {
                    case FunctionType.Sine:
                        GenerateMethodBody(generator, function.Arguments[0]);
                        
                        generator.Emit(OpCodes.Call, typeof(Math).GetMethod("Sin"));
                        break;
                    case FunctionType.Cosine:
                        GenerateMethodBody(generator, function.Arguments[0]);
                        
                        generator.Emit(OpCodes.Call, typeof(Math).GetMethod("Cos"));
                        break;
                    case FunctionType.Loge:
                        GenerateMethodBody(generator, function.Arguments[0]);

                        generator.Emit(OpCodes.Call, typeof(Math).GetMethod("Log", new Type[] { typeof(double) }));
                        break;
                    case FunctionType.Log10:
                        GenerateMethodBody(generator, function.Arguments[0]);
                        
                        generator.Emit(OpCodes.Call, typeof(Math).GetMethod("Log10"));
                        break;
                    case FunctionType.Logn:
                        GenerateMethodBody(generator, function.Arguments[0]);
                        GenerateMethodBody(generator, function.Arguments[1]);

                        generator.Emit(OpCodes.Call, typeof(Math).GetMethod("Log", new Type[] { typeof(double), typeof(double) }));
                        break;
                    default:
                        throw new ArgumentException(string.Format("Unsupported function \"{0}\".", function.FunctionType), "operation");
                }
            }
            else
            {
                throw new ArgumentException(string.Format("Unsupported operation \"{0}\".", operation.GetType().FullName), "operation");
            }
        }
    }
}
