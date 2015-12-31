﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Vibrant.InfluxDB.Client.Resources;

namespace Vibrant.InfluxDB.Client.Metadata
{
   internal static class MetadataCache
   {
      private static readonly object _sync = new object();
      private static readonly Dictionary<Type, object> _typeCache = new Dictionary<Type, object>();
      private static readonly HashSet<Type> _validFieldTypes = new HashSet<Type> { typeof( string ), typeof( double ), typeof( long ), typeof( bool ), typeof( DateTime ) };

      internal static InfluxRowTypeInfo<TInfluxRow> GetOrCreate<TInfluxRow>()
         where TInfluxRow : new()
      {
         lock ( _sync )
         {
            object cache;
            var type = typeof( TInfluxRow );

            if ( !_typeCache.TryGetValue( type, out cache ) )
            {
               var tags = new Dictionary<string, PropertyExpressionInfo<TInfluxRow>>( StringComparer.InvariantCulture );
               var fields = new Dictionary<string, PropertyExpressionInfo<TInfluxRow>>( StringComparer.InvariantCulture );
               var all = new Dictionary<string, PropertyExpressionInfo<TInfluxRow>>( StringComparer.InvariantCulture );
               PropertyExpressionInfo<TInfluxRow> timestamp = null;
               foreach ( var propertyInfo in type.GetProperties( BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public ) )
               {
                  var fieldAttribute = propertyInfo.GetCustomAttribute<InfluxFieldAttribute>();
                  var tagAttribute = propertyInfo.GetCustomAttribute<InfluxTagAttribute>();
                  var timestampAttribute = propertyInfo.GetCustomAttribute<InfluxTimestampAttribute>();

                  // list all attributes so we can ensure the attributes specified on a property are valid
                  var allAttributes = new Attribute[] { fieldAttribute, tagAttribute, timestampAttribute }
                     .Where( x => x != null )
                     .ToList();

                  if ( allAttributes.Count > 1 )
                  {
                     throw new InfluxException( string.Format( Errors.MultipleAttributesOnSingleProperty, propertyInfo.Name, type.Name ) );
                  }

                  if ( timestampAttribute != null )
                  {
                     timestamp = new PropertyExpressionInfo<TInfluxRow>( "time", propertyInfo );
                     if ( timestamp.Type != typeof( DateTime ) )
                     {
                        throw new InfluxException( string.Format( Errors.InvalidTimestampType, propertyInfo.Name, type.Name ) );
                     }

                     all.Add( "time", timestamp );
                  }
                  else if ( fieldAttribute != null )
                  {
                     var expression = new PropertyExpressionInfo<TInfluxRow>( fieldAttribute.Name, propertyInfo );
                     if ( !_validFieldTypes.Contains( expression.Type ) && !expression.Type.IsEnum )
                     {
                        throw new InfluxException( string.Format( Errors.InvalidFieldType, propertyInfo.Name, type.Name ) );
                     }

                     if ( string.IsNullOrEmpty( fieldAttribute.Name ) )
                     {
                        throw new InfluxException( string.Format( Errors.InvalidNameProperty, propertyInfo.Name, type.Name ) );
                     }

                     fields.Add( fieldAttribute.Name, expression );
                     all.Add( fieldAttribute.Name, expression );
                  }
                  else if ( tagAttribute != null )
                  {
                     var expression = new PropertyExpressionInfo<TInfluxRow>( tagAttribute.Name, propertyInfo );
                     if ( expression.Type != typeof( string ) && !expression.Type.IsEnum )
                     {
                        throw new InfluxException( string.Format( Errors.InvalidTagType, propertyInfo.Name, type.Name ) );
                     }

                     if ( string.IsNullOrEmpty( tagAttribute.Name ) )
                     {
                        throw new InfluxException( string.Format( Errors.InvalidNameProperty, propertyInfo.Name, type.Name ) );
                     }

                     tags.Add( tagAttribute.Name, expression );
                     all.Add( tagAttribute.Name, expression );
                  }
               }

               cache = new InfluxRowTypeInfo<TInfluxRow>( timestamp, tags, fields, all );

               _typeCache.Add( typeof( TInfluxRow ), cache );
            }
            return (InfluxRowTypeInfo<TInfluxRow>)cache;
         }
      }
   }
}
