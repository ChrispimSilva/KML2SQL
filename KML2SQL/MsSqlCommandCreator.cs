﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Windows.Documents;
using Microsoft.SqlServer.Types;
using SharpKml.Base;
using SharpKml.Dom;
using SharpKml.Engine;

namespace KML2SQL
{
    static class MsSqlCommandCreator
    {
        public static SqlCommand CreateCommand(MapFeature mapFeature, bool geographyMode, int srid, string tableName, 
            string placemarkColumnName, SqlConnection connection)
        {
            StringBuilder sbColumns = new StringBuilder();
            StringBuilder sbValues = new StringBuilder();
            foreach (KeyValuePair<string, string> simpleData in mapFeature.Data)
            {
                sbColumns.Append(simpleData.Key + ",");
                sbValues.Append("@" + simpleData.Key + ",");
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(ParseCoordinates(srid, mapFeature, geographyMode));
            sb.Append(string.Format("INSERT INTO {0}(Id,{1}{2}) VALUES(@Id,{3}@placemark)", tableName, sbColumns, placemarkColumnName, sbValues));
            string sqlCommandText = sb.ToString();
            SqlCommand sqlCommand = new SqlCommand(sqlCommandText, connection);
            sqlCommand.Parameters.AddWithValue("@Id", mapFeature.Id);
            foreach (KeyValuePair<string, string> simpleData in mapFeature.Data)
            {
                sqlCommand.Parameters.AddWithValue("@" + simpleData.Key, simpleData.Value);
            }
            //string UdtType;
            //if (geographyMode)
            //    UdtType = "Geography";
            //else
            //    UdtType = "Geometry";

            //sqlCommand.Parameters.Add(new SqlParameter("@placemark", parseCoordinates(srid, mapFeature, geographyMode)) { UdtTypeName = UdtType });
            return sqlCommand;
        }

        private static string ParseCoordinates(int srid, MapFeature mapFeature, bool geographyMode)
        {
            StringBuilder commandString = new StringBuilder();
            if (geographyMode)
            {
                commandString.Append(ParseCoordinatesGeography(srid, mapFeature));
                commandString.Append("DECLARE @placemark geography;");
                commandString.Append("SET @placemark = @validGeo;");
            }
            else
            {
                commandString.Append(ParseCoordinatesGeometry(srid, mapFeature, false));
                commandString.Append("DECLARE @placemark geometry;");
                commandString.Append("SET @placemark = @validGeom;");
            }
            return commandString.ToString();
        }

        private static string ParseCoordinatesGeometry(int srid, MapFeature mapFeature, bool geographyMode)
        {
            StringBuilder commandString = new StringBuilder();
            switch (mapFeature.GeometryType)
            {
                case OpenGisGeometryType.Polygon:
                    {
                        commandString.Append(@"DECLARE @geom geometry;
                                        SET @geom = geometry::STPolyFromText('POLYGON((");
                        foreach (Vector coordinate in mapFeature.Coordinates)
                        {
                            commandString.Append(coordinate.Longitude + " " + coordinate.Latitude + ", ");
                        }
                        commandString.Remove(commandString.Length - 2, 2).ToString();

                        foreach (Vector[] innerCoordinates in mapFeature.InnerCoordinates)
                        {
                            commandString.Append("), (");
                            foreach (Vector coordinate in innerCoordinates)
                            {
                                commandString.Append(coordinate.Longitude + " " + coordinate.Latitude + ", ");
                            }
                            commandString.Remove(commandString.Length - 2, 2).ToString();
                        }

                        commandString.Append(@"))', " + srid + @");");
                        commandString.Append("DECLARE @validGeom geometry;");
                        if (geographyMode)
                        {
                            commandString.Append("SET @validGeom = @geom.MakeValid();");
                        }
                        else
                        {
                            commandString.Append("SET @validGeom = @geom.MakeValid().STUnion(@geom.STStartPoint());");
                        }
                    }
                    break;
                case OpenGisGeometryType.LineString:
                    {
                        commandString.Append(@"DECLARE @validGeom geometry;
                                    SET @validGeom = geometry::STLineFromText('LINESTRING (");
                        foreach (Vector coordinate in mapFeature.Coordinates)
                        {
                            commandString.Append(coordinate.Longitude + " " + coordinate.Latitude + ", ");
                        }
                        commandString.Remove(commandString.Length - 2, 2).ToString();
                        commandString.Append(@")', " + srid + @");");
                        //commandString.Append("DECLARE @validGeom geometry;");
                        //commandString.Append("SET @validGeom = @geom.MakeValid().STUnion(@geom.STStartPoint());");
                    }
                    break;
                case OpenGisGeometryType.Point:
                    {
                        commandString.Append(@"DECLARE @validGeom geometry;");
                        commandString.Append("SET @validGeom = geometry::STPointFromText('POINT (");
                        commandString.Append(mapFeature.Coordinates[0].Longitude + " " + mapFeature.Coordinates[0].Latitude);
                        commandString.Append(@")', " + srid + @");");
                    }
                    break;
                default:
                {
                    //Do nothing. It's probably polygon point we don't support.
                }
                    break;
            }
            return commandString.ToString();
        }

        private static string ParseCoordinatesGeography(int srid, MapFeature mapFeature)
        {
            StringBuilder commandString = new StringBuilder();
            commandString.Append(ParseCoordinatesGeometry(srid, mapFeature, true));
            commandString.Append("DECLARE @validGeo geography;");
            commandString.Append("SET @validGeo = geography::STGeomFromText(@validGeom.STAsText(), " + srid + @").MakeValid();");
            return commandString.ToString();
        }
    }
}
