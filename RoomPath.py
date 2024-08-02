import sqlite3
import pandas as pd
import os

# Lowering the security level of hard drives
import subprocess
command = 'icacls c:\ /setintegritylevel M'
subprocess.run(command, shell=True)

#Get Database Path
file_path = os.path.abspath('RoomPath.db')
print(file_path)


#Show all column data
pd.set_option('display.width',None)

#Read the Event table
conn = sqlite3.connect(file_path)
comment_data = pd.read_sql_query('select * from Event;', conn)
print(comment_data)

#Read the newest row of the table
conn = sqlite3.connect(file_path)
newest_data = pd.read_sql_query('select * from Event order by EventID DESC limit 1;', conn)
print(newest_data)

#Extract edge
conn = sqlite3.connect(file_path)
sql_query1 = """
WITH PairedData AS (
SELECT "ObjectID-b" AS value,
LEAD("ObjectID-b") OVER (ORDER BY "EventID") AS next_value
FROM EventView
WHERE "ObjectID-b" IS NOT NULL)
SELECT value AS room1,
next_value AS room2
FROM PairedData
WHERE next_value IS NOT NULL
ORDER BY value;
"""
edge_data = pd.read_sql_query(sql_query1, conn)
print(edge_data)

#Convert data to CSV (edge)
csv_file_path = 'ge_data.csv'
edge_data.to_csv(csv_file_path, index=False)

print(f'Data has been saved to {csv_file_path}')

#Extract node
conn = sqlite3.connect(file_path)
sql_query2 = """
SELECT "ObjectID-b" AS "RoomID",
ObjectName AS "Room Name",
MIN(WhenEventHappened) AS "When Room Created",
MAX(WhenEventHappened) AS "Newest Traverse",
EventDescription AS "Traverse Description"
FROM EventView
GROUP BY ObjectName
ORDER BY "Newest Traverse";
"""
node_data = pd.read_sql_query(sql_query2, conn)
print(node_data)

#Convert data to CSV (node)
csv_file_path2 = 'de_data.csv'
node_data.to_csv(csv_file_path2, index=False)

print(f'Data has been saved to {csv_file_path2}')

#Show output
input()
