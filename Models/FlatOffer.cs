using System;
using System.Collections.Generic;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Linq;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace FlatScraper.Models
{
    public class FlatOffer
    {
        [BsonId]
        //[BsonRepresentation(BsonType.ObjectId)]
        public string Link { get; set; }
        public FlatOfferState State { get; set; }
        public List<FlatOfferState> StateHistory { get; set; }

        public override bool Equals(Object obj)
        {
            return (obj is FlatOffer) && ((FlatOffer)obj).Link == Link && State.Equals(((FlatOffer)obj).State);
        }

        public override int GetHashCode()
        {
            return Link.GetHashCode();
        }

        public void AddState(FlatOfferState newState)
        {
            var timeStamp = DateTime.UtcNow;
            State.LastChecked = newState.Created;
            newState.LastChecked = timeStamp;
            if (StateHistory == null)
                StateHistory = new List<FlatOfferState>();
            StateHistory.Add(State);
            State = newState;
        }

        public void AddProperties(Dictionary<string, string> properties)
        {
            var newState = new FlatOfferState {
                Created = State.Created,
                Properties = properties
            };
            AddState(newState);
        }

    }

    public class FlatOfferState
    {
        public DateTime LastChecked { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Delisted { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        public override bool Equals(Object obj)
        {
            bool isEqual = obj is FlatOfferState;
            FlatOfferState objConverted = (FlatOfferState)obj;
            // isEqual = isEqual && objConverted.Created == Created;
            isEqual = isEqual && Properties.Keys.Count == objConverted.Properties.Keys.Count &&
                Properties.Keys.All(k => objConverted.Properties.ContainsKey(k) && object.Equals(objConverted.Properties[k], Properties[k]));

            return isEqual;
        }

        public override int GetHashCode()
        {
            return Properties.GetHashCode();
        }

        public FlatOfferState DeepCopy()
        {
            FlatOfferState deepCopy = new FlatOfferState {
                LastChecked = this.LastChecked,
                Created = this.Created,
                Delisted = this.Delisted,
                Properties = this.Properties,
            };
            return deepCopy;
        }
    }
}