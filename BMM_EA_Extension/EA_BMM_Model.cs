using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R4C_BMM_EA_Extension
{
    namespace EA_BMM_Model
    {
        public class EA_BMM_ModelFactory
        {
            private EA_BMM_ModelFactory()
            {
            }

            static public BMM_Package getPackage(EA.Repository eaRepos, EA.Package eaPackage)
            {
                BMM_Package package = new BMM_Package();
                package.bind(eaRepos, eaPackage);
                return package;
            }
        }

        public abstract class Base
        {
            protected EA.Repository eaRepos;
            protected EA.Element eaElement;

            public string name
            {
                get
                {
                    if (eaElement != null)
                    {
                        return eaElement.Name.Replace(' ', '_');
                    }
                    else
                    {
                        return "";
                    }
                }
            }

            public bool is_archetype_parent
            {
                get
                {
                    return "archetype_parent".Equals(eaElement.StereotypeEx);
                }
            }

            public void bind(EA.Repository eaRepos, EA.Element eaElement)
            {
                this.eaRepos = eaRepos;
                this.eaElement = eaElement;
            }

            abstract public void accept(IVisitor visitor);
        }

        public class BMM_Package : Base
        {
            protected EA.Package eaPackage;

            public string rm_release
            {
                get { return eaElement.Version; }
            }

            public string rm_publisher
            {
                get { return eaElement.Author; }
            }

            public string schema_name
            {
                get { return eaElement.Alias; }
            }

            public string schema_lifecycle_state
            {
                get { return eaElement.Status; }
            }

            public bool has_child_packages
            {
                get
                {
                    return eaPackage.Packages.Count > 0;
                }
            }

            public bool is_archetype_rm_closure_package
            {
                get
                {
                    return "archetype_rm_closure".Equals(eaPackage.StereotypeEx);
                }
            }

            public bool is_primitive_types_package
            {
                get
                {
                    return "primitive_types".Equals(eaPackage.StereotypeEx);
                }
            }

            public List<BMM_Package> packages
            {
                get
                {
                    List<BMM_Package> childs = new List<BMM_Package>();
                    foreach (EA.Package eaChild in eaPackage.Packages)
                    {
                        BMM_Package child = new BMM_Package();
                        child.bind(eaRepos, eaChild);
                        childs.Add(child);
                    }
                    return childs;
                }
            }

            public bool has_classes
            {
                get
                {
                    return eaPackage.Elements.Cast<EA.Element>()
                      .Count(obj => "Class".Equals(obj.Type) || "DataType".Equals(obj.Type)) > 0;
                }
            }

            public List<BMM_Class> classes
            {
                get
                {
                    // Only elements of type "Class"; ignore others
                    List<BMM_Class> childs = new List<BMM_Class>();
                    foreach (EA.Element eaChild in eaPackage.Elements.Cast<EA.Element>()
                      .Where(obj => "Class".Equals(obj.Type) || "DataType".Equals(obj.Type)))
                    {
                        // ignore <enumeration>'s for now
                        // TODO: output to OpenEHR valueset XML
                        if (!"enumeration".Equals(eaChild.Stereotype))
                        {
                            BMM_Class child = new BMM_Class();
                            child.bind(eaRepos, eaChild);
                            childs.Add(child);
                        }
                    }
                    return childs;
                }
            }

            public void bind(EA.Repository eaRepos, EA.Package eaPackage)
            {
                this.eaPackage = eaPackage;
                bind(eaRepos, eaPackage.Element);
            }

            override public void accept(IVisitor visitor)
            {
                visitor.visit(this);
            }
        }

        public class BMM_Class : Base
        {
            public bool is_abstract
            {
                get
                {
                    switch (eaElement.Abstract)
                    {
                        case "0":
                            return false;
                        case "1":
                            return true;
                        default: // should not happen
                            return false;
                    }
                }
            }

            public bool is_open
            {
                get
                {
                    return eaElement.Subtype == 1; // Parameterised
                }
            }

            public bool has_ancestors
            {
                get
                {
                    return eaElement.BaseClasses.Count > 0;
                }
            }

            public List<BMM_Class> ancestors
            {
                get
                {
                    List<BMM_Class> ancestors = new List<BMM_Class>();
                    foreach (EA.Element eaParent in eaElement.BaseClasses)
                    {
                        BMM_Class ancestor = new BMM_Class();
                        ancestor.bind(eaRepos, eaParent);
                        ancestors.Add(ancestor);
                    }
                    return ancestors;
                }
            }

            public bool has_properties
            {
                get
                {
                    return (eaElement.Attributes.Count 
                       + eaElement.Connectors.Cast<EA.Connector>().Count(con => "Association".Equals(con.Type) && con.SupplierID != eaElement.ElementID) 
                       + eaElement.Connectors.Cast<EA.Connector>().Count(con => "Aggregation".Equals(con.Type) && con.ClientID != eaElement.ElementID)
                       ) > 0;
                }
            }

            public List<BMM_Property> properties
            {
                get
                {
                    List<BMM_Property> properties = new List<BMM_Property>();
                    foreach (EA.Attribute eaAttribute in eaElement.Attributes)
                    {
                        BMM_Property_FromAttribute property = new BMM_Property_FromAttribute();
                        property.bind(eaRepos, eaAttribute, this);
                        properties.Add(property);
                    }

                    // Add associations as properties
                    // Don't know why, but there are a lot of connector to self
                    // Only do "Associations" and "Aggregations" and ignore "Generalization" and others
                    foreach (EA.Connector eaConnector in eaElement.Connectors.Cast<EA.Connector>()
                      .Where(con => "Association".Equals(con.Type) && con.SupplierID != eaElement.ElementID))
                    {
                        BMM_Property_FromAssociation property = new BMM_Property_FromAssociation();
                        property.bind(eaRepos, eaConnector, this);
                        properties.Add(property);
                    }
                    foreach (EA.Connector eaConnector in eaElement.Connectors.Cast<EA.Connector>()
                      .Where(con => "Aggregation".Equals(con.Type) && con.ClientID != eaElement.ElementID))
                    {
                        BMM_Property_FromAggregation property = new BMM_Property_FromAggregation();
                        property.bind(eaRepos, eaConnector, this);
                        properties.Add(property);
                    }
                    return properties;
                }
            }

            override public void accept(IVisitor visitor)
            {
                visitor.visit(this);
            }
        }

        public abstract class BMM_Property
        {
            public const string P_BMM_CONTAINER_PROPERTY = "P_BMM_CONTAINER_PROPERTY";
            public const string P_BMM_SINGLE_PROPERTY = "P_BMM_SINGLE_PROPERTY";
            public const string P_BMM_GENERIC_PROPERTY = "P_BMM_GENERIC_PROPERTY";
            public const string P_BMM_SINGLE_PROPERTY_OPEN = "P_BMM_SINGLE_PROPERTY_OPEN";

            protected BMM_Class parent;
            protected string uml_name;
            protected string uml_type;
            protected string uml_stereotype;
            protected string uml_cardinality;
            protected string uml_container;
            protected bool uml_derived;

            public virtual string kind
            {
                get
                {
                    if (parent.is_open)
                    {
                        return P_BMM_SINGLE_PROPERTY_OPEN;
                    }
                    else
                    {
                        switch (uml_cardinality)
                        {
                            case "1..*":
                            case "0..*":
                            case "*":
                                return P_BMM_CONTAINER_PROPERTY;
                            case "1..1":
                            case "0..1":
                            case "1":
                                return P_BMM_SINGLE_PROPERTY;
                            default:
                                return null;
                        }
                    }
                }
            }

            public string cardinality
            {
                get
                {
                    switch (uml_cardinality)
                    {
                        case "1..*":
                            return ">=1";
                        case "0..1": // mandatory = false
                            return null;
                        case "*":
                        case "0..*":
                            return ">=0";
                        case "1":
                        case "1..1":
                        default: // mandatory = true
                            return null;
                    }
                }
            }

            public bool is_mandatory
            {
                get
                {
                    switch (uml_cardinality)
                    {
                        case "1..*":
                        case "1..1":
                        case "1":
                            return true;
                        default:
                            return false;
                    }
                }
            }

            public bool is_computed { get { return uml_derived; } }

            public string container_type
            {
                get
                {
                    if (string.IsNullOrEmpty(uml_container))
                    {
                        return "List";
                    }
                    else
                    {
                        return uml_container;
                    }
                }
            }

            public string name
            {
                get 
                {
                    if (parent.is_open)
                    {
                        // remove <T> part from name
                        int lt = uml_name.IndexOf('<');
                        if (lt < 0) lt = uml_name.Length;
                        return uml_name.Substring(0, lt);
                    }
                    else
                    {
                        return uml_name.Replace(' ', '_');
                    }
                }
            }
            public string type { get { return uml_type; } }
            public bool is_im_runtime { get { return "is_im_runtime".Equals(uml_stereotype); } }
            public bool is_im_infrastructure { get { return "is_im_infrastructure".Equals(uml_stereotype); } }
        }

        public class BMM_Property_FromAttribute : BMM_Property
        {
            protected EA.Repository eaRepos;
            protected EA.Attribute eaAttribute;

            public void bind(EA.Repository eaRepos, EA.Attribute eaAttribute, BMM_Class parent)
            {
                this.parent = parent;
                this.eaRepos = eaRepos;
                this.eaAttribute = eaAttribute;
                uml_name = eaAttribute.Name;
                uml_type = eaAttribute.Type;
                uml_stereotype = eaAttribute.Stereotype;
                uml_cardinality = string.Format("{0}..{1}", eaAttribute.LowerBound, eaAttribute.UpperBound);
                uml_container = eaAttribute.Container;
                uml_derived = eaAttribute.IsDerived;
            }

            public override string kind
            {
                get
                {
                    // INTERVAL<DATE_TIME>
                    if (type.IndexOf('<') != -1)
                    {
                        if (parent.is_open)
                        {
                            return P_BMM_SINGLE_PROPERTY_OPEN;
                        }
                        else
                        {
                            return P_BMM_GENERIC_PROPERTY;
                        }
                    }
                    else if (eaAttribute.IsCollection)
                    {
                        return P_BMM_CONTAINER_PROPERTY;
                    }
                    else
                    {
                        switch (uml_cardinality)
                        {
                            case "1..*":
                            case "0..*":
                            case "*":
                                return P_BMM_CONTAINER_PROPERTY;
                            case "1..1":
                            case "0..1":
                            case "1":
                                return P_BMM_SINGLE_PROPERTY;
                            default:
                                return null;
                        }
                    }
                }
            }
        }

        public class BMM_Property_FromAssociation : BMM_Property
        {
            protected EA.Repository eaRepos;
            protected EA.Connector eaConnector;

            public void bind(EA.Repository eaRepos, EA.Connector eaConnector, BMM_Class parent)
            {
                this.parent = parent;
                this.eaRepos = eaRepos;
                this.eaConnector = eaConnector;
                uml_name = eaConnector.SupplierEnd.Role;
                uml_type = eaRepos.GetElementByID(eaConnector.SupplierID).Name;
                uml_stereotype = eaConnector.SupplierEnd.Stereotype;
                uml_cardinality = eaConnector.SupplierEnd.Cardinality;
                uml_derived = eaConnector.SupplierEnd.Derived;
            }
        }

        public class BMM_Property_FromAggregation : BMM_Property
        {
            protected EA.Repository eaRepos;
            protected EA.Connector eaConnector;

            public void bind(EA.Repository eaRepos, EA.Connector eaConnector, BMM_Class parent)
            {
                this.parent = parent;
                this.eaRepos = eaRepos;
                this.eaConnector = eaConnector;
                this.uml_name = eaConnector.ClientEnd.Role;
                this.uml_type = eaRepos.GetElementByID(eaConnector.ClientID).Name;
                this.uml_stereotype = eaConnector.ClientEnd.Stereotype;
                this.uml_cardinality = eaConnector.ClientEnd.Cardinality;
            }
        }

        public interface IVisitor
        {
            void visit(BMM_Package package);
            void visit(BMM_Class element);
        }
    }
}
