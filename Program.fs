module Program

open System.Threading.Tasks
open System.IO
open SqlHydra.Query

let schemaStr = 
  File.ReadAllText (__SOURCE_DIRECTORY__ + @"\Schema.sql")

let openSqlite () = task {
  let conn = new Microsoft.Data.Sqlite.SqliteConnection("")
  do! conn.OpenAsync ()

  use createSchema = conn.CreateCommand(CommandText = schemaStr)
  let _ = createSchema.ExecuteNonQuery()
  return conn
}

let sharedSqlite db =
  let compiler = SqlKata.Compilers.SqliteCompiler()
  ContextType.Shared (new QueryContext (db, compiler))

let getMax db = task {
  let! maxByID = selectTask Schema.HydraReader.Read (sharedSqlite db) {
    for row in Schema.main.TestTable do
    select (maxBy row.Id)
  }

  return Seq.toList maxByID
}

let thisWorks () = task {
  use! db = openSqlite ()
  let! _ = insertTask (sharedSqlite db) {
    into Schema.main.TestTable   
    entity { Id = 0; Callsign = "Name" }
  }

  return! getMax db
}

let demonstrateProblem () = task {
  use! db = openSqlite ()

  return! getMax db
}


[<EntryPoint>]
let main argv = 
  printfn "%A" (thisWorks().Result)
  printfn "%A" (demonstrateProblem().Result)
  0


