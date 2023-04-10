cat Schema.sql | sqlite3.exe .\temp.sqlite3

dotnet sqlhydra-sqlite

rm .\temp.sqlite3
