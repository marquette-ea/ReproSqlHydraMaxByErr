// This code was generated by `SqlHydra.Sqlite` -- v1.2.1.0.
namespace Schema


[<AutoOpen>]
module ColumnReaders =
    type Column(reader: System.Data.IDataReader, getOrdinal: string -> int, column) =
            member __.Name = column
            member __.IsNull() = getOrdinal column |> reader.IsDBNull
            override __.ToString() = __.Name

    type RequiredColumn<'T, 'Reader when 'Reader :> System.Data.IDataReader>(reader: 'Reader, getOrdinal, getter: int -> 'T, column) =
            inherit Column(reader, getOrdinal, column)
            member __.Read(?alias) = alias |> Option.defaultValue __.Name |> getOrdinal |> getter

    type OptionalColumn<'T, 'Reader when 'Reader :> System.Data.IDataReader>(reader: 'Reader, getOrdinal, getter: int -> 'T, column) =
            inherit Column(reader, getOrdinal, column)
            member __.Read(?alias) = 
                match alias |> Option.defaultValue __.Name |> getOrdinal with
                | o when reader.IsDBNull o -> None
                | o -> Some (getter o)

    type RequiredBinaryColumn<'T, 'Reader when 'Reader :> System.Data.IDataReader>(reader: 'Reader, getOrdinal, getValue: int -> obj, column) =
            inherit Column(reader, getOrdinal, column)
            member __.Read(?alias) = alias |> Option.defaultValue __.Name |> getOrdinal |> getValue :?> byte[]

    type OptionalBinaryColumn<'T, 'Reader when 'Reader :> System.Data.IDataReader>(reader: 'Reader, getOrdinal, getValue: int -> obj, column) =
            inherit Column(reader, getOrdinal, column)
            member __.Read(?alias) = 
                match alias |> Option.defaultValue __.Name |> getOrdinal with
                | o when reader.IsDBNull o -> None
                | o -> Some (getValue o :?> byte[])
            
[<AutoOpen>]
module private DataReaderExtensions =
    type System.Data.IDataReader with
        member reader.GetDateOnly(ordinal: int) = 
            reader.GetDateTime(ordinal) |> System.DateOnly.FromDateTime
    
    type System.Data.Common.DbDataReader with
        member reader.GetTimeOnly(ordinal: int) = 
            reader.GetFieldValue(ordinal) |> System.TimeOnly.FromTimeSpan
        
        

module main =
    [<CLIMutable>]
    type TestTable = { Id: int; Callsign: string }

    let TestTable = SqlHydra.Query.Table.table<TestTable>

    module Readers =
        type TestTableReader(reader: System.Data.Common.DbDataReader, getOrdinal) =
            member __.Id = RequiredColumn(reader, getOrdinal, reader.GetInt32, "Id")
            member __.Callsign = RequiredColumn(reader, getOrdinal, reader.GetString, "Callsign")

            member __.Read() =
                { TestTable.Id = __.Id.Read()
                  Callsign = __.Callsign.Read() }

            member __.ReadIfNotNull() =
                if __.Id.IsNull() then None else Some(__.Read())

type HydraReader(reader: System.Data.Common.DbDataReader) =
    let mutable accFieldCount = 0
    let buildGetOrdinal fieldCount =
        let dictionary = 
            [0..reader.FieldCount-1] 
            |> List.map (fun i -> reader.GetName(i), i)
            |> List.sortBy snd
            |> List.skip accFieldCount
            |> List.take fieldCount
            |> dict
        accFieldCount <- accFieldCount + fieldCount
        fun col -> dictionary.Item col
        
    let lazymainTestTable = lazy (main.Readers.TestTableReader(reader, buildGetOrdinal 2))
    member __.``main.TestTable`` = lazymainTestTable.Value
    member private __.AccFieldCount with get () = accFieldCount and set (value) = accFieldCount <- value

    member private __.GetReaderByName(entity: string, isOption: bool) =
        match entity, isOption with
        | "main.TestTable", false -> __.``main.TestTable``.Read >> box
        | "main.TestTable", true -> __.``main.TestTable``.ReadIfNotNull >> box
        | _ -> failwith $"Could not read type '{entity}' because no generated reader exists."

    static member private GetPrimitiveReader(t: System.Type, reader: System.Data.Common.DbDataReader, isOpt: bool) =
        let wrap get (ord: int) = 
            if isOpt 
            then (if reader.IsDBNull ord then None else get ord |> Some) |> box 
            else get ord |> box 
        

        if t = typedefof<int16> then Some(wrap reader.GetInt16)
        else if t = typedefof<int> then Some(wrap reader.GetInt32)
        else if t = typedefof<double> then Some(wrap reader.GetDouble)
        else if t = typedefof<System.Single> then Some(wrap reader.GetDouble)
        else if t = typedefof<decimal> then Some(wrap reader.GetDecimal)
        else if t = typedefof<bool> then Some(wrap reader.GetBoolean)
        else if t = typedefof<byte> then Some(wrap reader.GetByte)
        else if t = typedefof<int64> then Some(wrap reader.GetInt64)
        else if t = typedefof<byte []> then Some(wrap reader.GetValue)
        else if t = typedefof<string> then Some(wrap reader.GetString)
        else if t = typedefof<System.DateTime> then Some(wrap reader.GetDateTime)
        else if t = typedefof<System.DateOnly> then Some(wrap reader.GetDateOnly)
        else if t = typedefof<System.TimeOnly> then Some(wrap reader.GetTimeOnly)
        else if t = typedefof<System.Guid> then Some(wrap reader.GetGuid)
        else None

    static member Read(reader: System.Data.Common.DbDataReader) = 
        let hydra = HydraReader(reader)
                    
        let getOrdinalAndIncrement() = 
            let ordinal = hydra.AccFieldCount
            hydra.AccFieldCount <- hydra.AccFieldCount + 1
            ordinal
            
        let buildEntityReadFn (t: System.Type) = 
            let t, isOpt = 
                if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Option<_>> 
                then t.GenericTypeArguments.[0], true
                else t, false
            
            match HydraReader.GetPrimitiveReader(t, reader, isOpt) with
            | Some primitiveReader -> 
                let ord = getOrdinalAndIncrement()
                fun () -> primitiveReader ord
            | None ->
                let nameParts = t.FullName.Split([| '.'; '+' |])
                let schemaAndType = nameParts |> Array.skip (nameParts.Length - 2) |> fun parts -> System.String.Join(".", parts)
                hydra.GetReaderByName(schemaAndType, isOpt)
            
        // Return a fn that will hydrate 'T (which may be a tuple)
        // This fn will be called once per each record returned by the data reader.
        let t = typeof<'T>
        if FSharp.Reflection.FSharpType.IsTuple(t) then
            let readEntityFns = FSharp.Reflection.FSharpType.GetTupleElements(t) |> Array.map buildEntityReadFn
            fun () ->
                let entities = readEntityFns |> Array.map (fun read -> read())
                Microsoft.FSharp.Reflection.FSharpValue.MakeTuple(entities, t) :?> 'T
        else
            let readEntityFn = t |> buildEntityReadFn
            fun () -> 
                readEntityFn() :?> 'T
        